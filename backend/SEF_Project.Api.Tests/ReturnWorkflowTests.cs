using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SEF_Project.Api.Controllers;
using SEF_Project.Api.Data;
using SEF_Project.Api.DTOs.Orders;
using SEF_Project.Api.Models;
using SEF_Project.Api.Models.Catalog;
using SEF_Project.Api.Models.Enums;
using SEF_Project.Api.Models.Orders;
using SEF_Project.Api.Services.Orders;

namespace SEF_Project.Api.Tests;

public class ReturnWorkflowTests
{
    private const string TShirtXsSku = "TSH-CLS-XS";

    [Fact]
    public async Task CreateReturn_ShouldPersistGroundedItemsAndRefundSnapshot()
    {
        await using var fixture = await ReturnFixture.CreateAsync();

        var response = await fixture.Service.CreateReturnAsync(
            fixture.CustomerUserId,
            false,
            fixture.Order.Id,
            Request(fixture.OrderItem.Id, 1));

        Assert.NotNull(response);
        Assert.Equal(ReturnStatus.Requested, response.Status);
        Assert.Equal(2500m, response.RefundAmount);
        Assert.StartsWith("RET-", response.ReturnNumber);
        var item = Assert.Single(response.Items);
        Assert.Equal(fixture.OrderItem.Id, item.OrderItemId);
        Assert.Equal(TShirtXsSku, item.Sku);
        Assert.Equal(2500m, item.LineRefundAmount);
    }

    [Fact]
    public async Task CreateReturn_ShouldRejectAnotherCustomersOrderAndExcessQuantity()
    {
        await using var fixture = await ReturnFixture.CreateAsync();
        var otherUserId = await fixture.AddCustomerAsync("other@test.com");

        var hidden = await fixture.Service.CreateReturnAsync(
            otherUserId,
            false,
            fixture.Order.Id,
            Request(fixture.OrderItem.Id, 1));
        Assert.Null(hidden);

        await fixture.Service.CreateReturnAsync(
            fixture.CustomerUserId,
            false,
            fixture.Order.Id,
            Request(fixture.OrderItem.Id, 1));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fixture.Service.CreateReturnAsync(
                fixture.CustomerUserId,
                false,
                fixture.Order.Id,
                Request(fixture.OrderItem.Id, 1)));
        Assert.Contains("remaining returnable quantity", exception.Message);
    }

    [Fact]
    public async Task StaffWorkflow_ShouldRestockThenRefundAndCloseFullyReturnedOrder()
    {
        await using var fixture = await ReturnFixture.CreateAsync();
        var stockBefore = fixture.Inventory.QuantityOnHand;
        var requested = await fixture.Service.CreateReturnAsync(
            fixture.CustomerUserId,
            false,
            fixture.Order.Id,
            Request(fixture.OrderItem.Id, 1));

        var approved = await fixture.Service.UpdateStatusAsync(
            fixture.StaffUserId,
            true,
            requested!.Id,
            new UpdateReturnStatusRequest { Status = ReturnStatus.Approved });
        Assert.Equal(ReturnStatus.Approved, approved!.Status);

        var received = await fixture.Service.UpdateStatusAsync(
            fixture.StaffUserId,
            true,
            requested.Id,
            new UpdateReturnStatusRequest { Status = ReturnStatus.Received });
        Assert.NotNull(received!.ReceivedAt);

        await fixture.Context.Entry(fixture.Inventory).ReloadAsync();
        Assert.Equal(stockBefore + 1, fixture.Inventory.QuantityOnHand);
        var stockReturn = await fixture.Context.InventoryTransactions
            .SingleAsync(transaction =>
                transaction.Type == InventoryTransactionType.Return);
        Assert.Equal(1, stockReturn.QuantityChange);
        Assert.Equal(requested.ReturnNumber, stockReturn.Reference);

        var refunded = await fixture.Service.UpdateStatusAsync(
            fixture.StaffUserId,
            true,
            requested.Id,
            new UpdateReturnStatusRequest { Status = ReturnStatus.Refunded });
        Assert.NotNull(refunded!.RefundedAt);

        var refundPayment = await fixture.Context.Payments
            .SingleAsync(payment =>
                payment.TransactionReference == $"RETURN-{requested.ReturnNumber}");
        Assert.Equal(PaymentStatus.Refunded, refundPayment.Status);
        Assert.Equal(2500m, refundPayment.Amount);

        var order = await fixture.Context.Orders
            .Include(value => value.StatusHistory)
            .SingleAsync(value => value.Id == fixture.Order.Id);
        Assert.Equal(OrderStatus.Refunded, order.Status);
        Assert.Contains(order.StatusHistory, history =>
            history.Status == OrderStatus.Refunded);
    }

    [Fact]
    public async Task Customer_ShouldOnlyCancelRequestedReturn()
    {
        await using var fixture = await ReturnFixture.CreateAsync();
        var requested = await fixture.Service.CreateReturnAsync(
            fixture.CustomerUserId,
            false,
            fixture.Order.Id,
            Request(fixture.OrderItem.Id, 1));

        var cancelled = await fixture.Service.CancelReturnAsync(
            fixture.CustomerUserId,
            false,
            requested!.Id,
            new CancelReturnRequest());
        Assert.Equal(ReturnStatus.Cancelled, cancelled!.Status);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fixture.Service.CancelReturnAsync(
                fixture.CustomerUserId,
                false,
                requested.Id,
                new CancelReturnRequest()));
        Assert.Contains("pending return", exception.Message);
    }

    [Fact]
    public void ReturnController_ShouldEnforceRoleBoundaries()
    {
        var create = typeof(ReturnsController)
            .GetMethod(nameof(ReturnsController.CreateReturn))!
            .GetCustomAttribute<AuthorizeAttribute>();
        var update = typeof(ReturnsController)
            .GetMethod(nameof(ReturnsController.UpdateStatus))!
            .GetCustomAttribute<AuthorizeAttribute>();

        Assert.Equal("Customer", create!.Roles);
        Assert.Equal("Staff,Administrator", update!.Roles);
    }

    private static CreateReturnRequest Request(Guid orderItemId, int quantity) =>
        new()
        {
            Reason = ReturnReason.WrongSize,
            Note = "The fit was too small.",
            Items = new List<CreateReturnItemRequest>
            {
                new() { OrderItemId = orderItemId, Quantity = quantity }
            }
        };

    private sealed class ReturnFixture : IAsyncDisposable
    {
        private ReturnFixture(
            SqliteConnection connection,
            AppDbContext context,
            ReturnService service,
            int customerUserId,
            int staffUserId,
            Order order,
            OrderItem orderItem,
            InventoryStock inventory)
        {
            Connection = connection;
            Context = context;
            Service = service;
            CustomerUserId = customerUserId;
            StaffUserId = staffUserId;
            Order = order;
            OrderItem = orderItem;
            Inventory = inventory;
        }

        public SqliteConnection Connection { get; }
        public AppDbContext Context { get; }
        public ReturnService Service { get; }
        public int CustomerUserId { get; }
        public int StaffUserId { get; }
        public Order Order { get; }
        public OrderItem OrderItem { get; }
        public InventoryStock Inventory { get; }

        public static async Task<ReturnFixture> CreateAsync()
        {
            var connection = new SqliteConnection("DataSource=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .Options;
            var context = new AppDbContext(options);
            await context.Database.EnsureCreatedAsync();

            var customerUserId = await AddUserAsync(
                context,
                "customer@test.com",
                roleId: 1,
                withCustomer: true);
            var staffUserId = await AddUserAsync(
                context,
                "staff@test.com",
                roleId: 2,
                withCustomer: false);
            var customer = await context.Customers
                .SingleAsync(value => value.UserId == customerUserId);
            var variant = await context.ProductVariants
                .FirstAsync(value => value.Sku == TShirtXsSku);
            var inventory = await context.Inventory
                .SingleAsync(value => value.ProductVariantId == variant.Id);

            var order = new Order
            {
                OrderNumber = "ORD-RETURN-TEST",
                CustomerId = customer.Id,
                Status = OrderStatus.Completed,
                Subtotal = 2500m,
                Total = 2500m,
                Currency = "LKR",
                PlacedAt = DateTime.UtcNow.AddDays(-2)
            };
            var orderItem = new OrderItem
            {
                ProductVariantId = variant.Id,
                Quantity = 1,
                UnitPrice = 2500m,
                LineTotal = 2500m
            };
            order.Items.Add(orderItem);
            order.Payments.Add(new Payment
            {
                Amount = 2500m,
                Method = PaymentMethod.Card,
                Status = PaymentStatus.Completed,
                PaidAt = DateTime.UtcNow.AddDays(-2)
            });
            context.Orders.Add(order);
            await context.SaveChangesAsync();

            return new ReturnFixture(
                connection,
                context,
                new ReturnService(context, NullLogger<ReturnService>.Instance),
                customerUserId,
                staffUserId,
                order,
                orderItem,
                inventory);
        }

        public Task<int> AddCustomerAsync(string email) =>
            AddUserAsync(Context, email, roleId: 1, withCustomer: true);

        private static async Task<int> AddUserAsync(
            AppDbContext context,
            string email,
            int roleId,
            bool withCustomer)
        {
            var user = new User
            {
                Email = email,
                PasswordHash = "hash",
                FirstName = "Test",
                LastName = "User",
                RoleId = roleId,
                IsActive = true
            };
            context.Users.Add(user);
            if (withCustomer)
            {
                context.Customers.Add(new Customer { User = user });
            }
            await context.SaveChangesAsync();
            return user.Id;
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await Connection.DisposeAsync();
        }
    }
}
