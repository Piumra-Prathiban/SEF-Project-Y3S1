using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SEF_Project.Api.Data;
using SEF_Project.Api.DTOs.Orders;
using SEF_Project.Api.Models;
using SEF_Project.Api.Models.Enums;
using SEF_Project.Api.Models.Orders;
using SEF_Project.Api.Services.Orders;

namespace SEF_Project.Api.Tests;

public class CancellationWorkflowTests
{
    private const string MargheritaSmallSku = "PIZ-MARG-S";

    private static async Task<AppDbContext> CreateContextAsync(
        SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();

        return context;
    }

    private static async Task<(int UserId, int CustomerId)> SeedCustomerAsync(
        AppDbContext context,
        string email)
    {
        var user = new User
        {
            Email = email,
            PasswordHash = "not-a-real-hash",
            FirstName = "Test",
            LastName = "Customer",
            RoleId = 1,
            IsActive = true
        };

        context.Users.Add(user);
        context.Customers.Add(new Customer { User = user });

        await context.SaveChangesAsync();

        var customer = await context.Customers
            .SingleAsync(c => c.UserId == user.Id);

        return (user.Id, customer.Id);
    }

    private static CreateOrderAddressRequest ValidAddress() =>
        new()
        {
            FullName = "Test Customer",
            Line1 = "12 Main Street",
            City = "Colombo",
            PostalCode = "00100"
        };

    private static OrderService CreateService(AppDbContext context) =>
        new(context, NullLogger<OrderService>.Instance);

    private static async Task<OrderResponse> PlaceOrderAsync(
        OrderService service,
        int userId,
        Guid variantId,
        int quantity)
    {
        return await service.CreateOrderAsync(
            userId,
            new CreateOrderRequest
            {
                Items = new List<CreateOrderItemRequest>
                {
                    new() { ProductVariantId = variantId, Quantity = quantity }
                },
                DeliveryAddress = ValidAddress(),
                PaymentMethod = PaymentMethod.Card
            });
    }

    [Fact]
    public async Task CancelOrderAsync_ShouldCancelOrder_AndRecordHistory()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var context = await CreateContextAsync(connection);

        var (customerUserId, _) =
            await SeedCustomerAsync(context, "customer@test.com");
        var variant = await context.ProductVariants
            .FirstAsync(v => v.Sku == MargheritaSmallSku);

        var service = CreateService(context);
        var order = await PlaceOrderAsync(
            service,
            customerUserId,
            variant.Id,
            2);

        var cancelled = await service.CancelOrderAsync(
            customerUserId,
            false,
            order.Id,
            new CancelOrderRequest { Reason = "Changed my mind." });

        Assert.NotNull(cancelled);
        Assert.Equal(OrderStatus.Cancelled, cancelled.Status);

        var persisted = await context.Orders.AsNoTracking()
            .SingleAsync();
        Assert.Equal(OrderStatus.Cancelled, persisted.Status);

        var history = await context.OrderStatusHistory.AsNoTracking()
            .SingleAsync(h => h.Status == OrderStatus.Cancelled);
        Assert.Equal(customerUserId, history.ChangedByUserId);
        Assert.Equal("Changed my mind.", history.Note);
    }

    [Fact]
    public async Task CancelOrderAsync_ShouldReleaseInventory()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var context = await CreateContextAsync(connection);

        var (customerUserId, _) =
            await SeedCustomerAsync(context, "customer@test.com");
        var variant = await context.ProductVariants
            .FirstAsync(v => v.Sku == MargheritaSmallSku);

        var service = CreateService(context);
        var order = await PlaceOrderAsync(
            service,
            customerUserId,
            variant.Id,
            2);

        var reservedBefore = (await context.Inventory
            .SingleAsync(i => i.ProductVariantId == variant.Id))
            .ReservedQuantity;
        Assert.Equal(2, reservedBefore);

        await service.CancelOrderAsync(
            customerUserId,
            false,
            order.Id,
            new CancelOrderRequest());

        var inventoryAfter = await context.Inventory.AsNoTracking()
            .SingleAsync(i => i.ProductVariantId == variant.Id);
        Assert.Equal(0, inventoryAfter.ReservedQuantity);

        var release = await context.InventoryTransactions.AsNoTracking()
            .SingleAsync(
                t => t.Type == InventoryTransactionType.ReservationRelease);

        Assert.Equal(2, release.QuantityChange);
        Assert.Equal(
            inventoryAfter.QuantityOnHand,
            release.QuantityOnHandAfter);
    }

    [Fact]
    public async Task CancelOrderAsync_ShouldRefundCompleted_AndFailPending()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var context = await CreateContextAsync(connection);

        var (customerUserId, _) =
            await SeedCustomerAsync(context, "customer@test.com");
        var (staffUserId, _) =
            await SeedCustomerAsync(context, "staff@test.com");
        var variant = await context.ProductVariants
            .FirstAsync(v => v.Sku == MargheritaSmallSku);

        var service = CreateService(context);
        var order = await PlaceOrderAsync(
            service,
            customerUserId,
            variant.Id,
            2);

        var completedPayment = await service.CreatePaymentAsync(
            customerUserId,
            false,
            order.Id,
            new CreatePaymentRequest
            {
                Method = PaymentMethod.Card,
                Amount = 2000m
            });
        await service.UpdatePaymentStatusAsync(
            staffUserId,
            true,
            order.Id,
            completedPayment!.Id,
            new UpdatePaymentStatusRequest
            {
                Status = PaymentStatus.Completed
            });

        var pendingPayment = await service.CreatePaymentAsync(
            customerUserId,
            false,
            order.Id,
            new CreatePaymentRequest
            {
                Method = PaymentMethod.Cash,
                Amount = 400m
            });

        await service.CancelOrderAsync(
            customerUserId,
            false,
            order.Id,
            new CancelOrderRequest());

        var payments = await context.Payments.AsNoTracking()
            .ToListAsync();

        Assert.Contains(
            payments,
            p => p.Id == completedPayment.Id
                && p.Status == PaymentStatus.Refunded);
        Assert.Contains(
            payments,
            p => p.Id == pendingPayment!.Id
                && p.Status == PaymentStatus.Failed);
    }

    [Fact]
    public async Task CancelOrderAsync_ShouldRejectInvalidStates()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var context = await CreateContextAsync(connection);

        var (customerUserId, customerId) =
            await SeedCustomerAsync(context, "customer@test.com");

        var completed = new Order
        {
            OrderNumber = $"ORD-{Guid.NewGuid():N}",
            CustomerId = customerId,
            Status = OrderStatus.Completed,
            Subtotal = 100m,
            Total = 100m,
            PlacedAt = DateTime.UtcNow
        };
        var cancelled = new Order
        {
            OrderNumber = $"ORD-{Guid.NewGuid():N}",
            CustomerId = customerId,
            Status = OrderStatus.Cancelled,
            Subtotal = 100m,
            Total = 100m,
            PlacedAt = DateTime.UtcNow
        };

        context.Orders.AddRange(completed, cancelled);
        await context.SaveChangesAsync();

        var service = CreateService(context);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CancelOrderAsync(
                customerUserId,
                false,
                completed.Id,
                new CancelOrderRequest()));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CancelOrderAsync(
                customerUserId,
                false,
                cancelled.Id,
                new CancelOrderRequest()));
    }

    [Fact]
    public async Task CancelOrderAsync_ShouldRejectNonOwnerCustomer()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var context = await CreateContextAsync(connection);

        var (customerUserIdA, _) =
            await SeedCustomerAsync(context, "a@test.com");
        var (customerUserIdB, _) =
            await SeedCustomerAsync(context, "b@test.com");
        var (staffUserId, _) =
            await SeedCustomerAsync(context, "staff@test.com");
        var variant = await context.ProductVariants
            .FirstAsync(v => v.Sku == MargheritaSmallSku);

        var service = CreateService(context);
        var order = await PlaceOrderAsync(
            service,
            customerUserIdA,
            variant.Id,
            1);

        var denied = await service.CancelOrderAsync(
            customerUserIdB,
            false,
            order.Id,
            new CancelOrderRequest());

        Assert.Null(denied);

        var allowed = await service.CancelOrderAsync(
            staffUserId,
            true,
            order.Id,
            new CancelOrderRequest());

        Assert.NotNull(allowed);
        Assert.Equal(OrderStatus.Cancelled, allowed.Status);
    }

    [Fact]
    public async Task CancelOrderAsync_ShouldRollback_OnInconsistentInventory()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var context = await CreateContextAsync(connection);

        var (customerUserId, customerId) =
            await SeedCustomerAsync(context, "customer@test.com");
        var variant = await context.ProductVariants
            .FirstAsync(v => v.Sku == MargheritaSmallSku);

        var order = new Order
        {
            OrderNumber = $"ORD-{Guid.NewGuid():N}",
            CustomerId = customerId,
            Status = OrderStatus.Confirmed,
            Subtotal = 500m,
            Total = 500m,
            PlacedAt = DateTime.UtcNow
        };

        context.Orders.Add(order);
        context.OrderItems.Add(new OrderItem
        {
            OrderId = order.Id,
            ProductVariantId = variant.Id,
            Quantity = 5,
            UnitPrice = 100m,
            LineTotal = 500m
        });
        await context.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateService(context).CancelOrderAsync(
                customerUserId,
                false,
                order.Id,
                new CancelOrderRequest()));

        var persisted = await context.Orders.AsNoTracking()
            .SingleAsync();
        Assert.Equal(OrderStatus.Confirmed, persisted.Status);

        Assert.Empty(await context.OrderStatusHistory
            .Where(h => h.OrderId == order.Id)
            .ToListAsync());
        Assert.Empty(await context.InventoryTransactions
            .Where(t => t.Type == InventoryTransactionType.ReservationRelease)
            .ToListAsync());
    }
}
