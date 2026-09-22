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

public class PaymentWorkflowTests
{
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

    private static async Task<Order> SeedOrderAsync(
        AppDbContext context,
        int customerId,
        decimal total,
        OrderStatus status = OrderStatus.Pending)
    {
        var order = new Order
        {
            OrderNumber = $"ORD-{Guid.NewGuid():N}",
            CustomerId = customerId,
            Status = status,
            Subtotal = total,
            Total = total,
            PlacedAt = DateTime.UtcNow
        };

        context.Orders.Add(order);
        await context.SaveChangesAsync();

        return order;
    }

    private static OrderService CreateService(AppDbContext context) =>
        new(context, NullLogger<OrderService>.Instance);

    [Fact]
    public async Task CreatePaymentAsync_ShouldRecordPendingPayment()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var context = await CreateContextAsync(connection);

        var (userId, customerId) =
            await SeedCustomerAsync(context, "customer@test.com");
        var order = await SeedOrderAsync(context, customerId, 1000m);

        var service = CreateService(context);

        var response = await service.CreatePaymentAsync(
            userId,
            false,
            order.Id,
            new CreatePaymentRequest
            {
                Method = PaymentMethod.Card,
                Amount = 400m
            });

        Assert.NotNull(response);
        Assert.Equal(PaymentStatus.Pending, response.Status);
        Assert.Equal(400m, response.Amount);
        Assert.Equal(PaymentMethod.Card, response.Method);
        Assert.Null(response.PaidAt);
    }

    [Fact]
    public async Task UpdatePaymentStatusAsync_ShouldCompletePayment()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var context = await CreateContextAsync(connection);

        var (customerUserId, customerId) =
            await SeedCustomerAsync(context, "customer@test.com");
        var (staffUserId, _) =
            await SeedCustomerAsync(context, "staff@test.com");
        var order = await SeedOrderAsync(context, customerId, 1000m);

        var service = CreateService(context);

        var payment = await service.CreatePaymentAsync(
            customerUserId,
            false,
            order.Id,
            new CreatePaymentRequest
            {
                Method = PaymentMethod.Card,
                Amount = 1000m
            });

        var completed = await service.UpdatePaymentStatusAsync(
            staffUserId,
            true,
            order.Id,
            payment!.Id,
            new UpdatePaymentStatusRequest
            {
                Status = PaymentStatus.Completed
            });

        Assert.NotNull(completed);
        Assert.Equal(PaymentStatus.Completed, completed.Status);
        Assert.NotNull(completed.PaidAt);
        Assert.False(string.IsNullOrWhiteSpace(completed.TransactionReference));
    }

    [Fact]
    public async Task CreatePaymentAsync_ShouldRejectInvalidAmounts()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var context = await CreateContextAsync(connection);

        var (userId, customerId) =
            await SeedCustomerAsync(context, "customer@test.com");
        var order = await SeedOrderAsync(context, customerId, 1000m);

        var service = CreateService(context);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreatePaymentAsync(
                userId,
                false,
                order.Id,
                new CreatePaymentRequest
                {
                    Method = PaymentMethod.Card,
                    Amount = 0m
                }));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreatePaymentAsync(
                userId,
                false,
                order.Id,
                new CreatePaymentRequest
                {
                    Method = PaymentMethod.Card,
                    Amount = 1000.01m
                }));
    }

    [Fact]
    public async Task CreatePaymentAsync_ShouldRejectNonOwnerCustomer()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var context = await CreateContextAsync(connection);

        var (_, customerIdA) =
            await SeedCustomerAsync(context, "a@test.com");
        var (userIdB, _) =
            await SeedCustomerAsync(context, "b@test.com");
        var order = await SeedOrderAsync(context, customerIdA, 1000m);

        var response = await CreateService(context).CreatePaymentAsync(
            userIdB,
            false,
            order.Id,
            new CreatePaymentRequest
            {
                Method = PaymentMethod.Cash,
                Amount = 100m
            });

        Assert.Null(response);
    }

    [Fact]
    public async Task CreatePaymentAsync_StaffCanRecordPaymentForAnyOrder()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var context = await CreateContextAsync(connection);

        var (_, customerId) =
            await SeedCustomerAsync(context, "customer@test.com");
        var (staffUserId, _) =
            await SeedCustomerAsync(context, "staff@test.com");
        var order = await SeedOrderAsync(context, customerId, 1000m);

        var response = await CreateService(context).CreatePaymentAsync(
            staffUserId,
            true,
            order.Id,
            new CreatePaymentRequest
            {
                Method = PaymentMethod.Cash,
                Amount = 1000m
            });

        Assert.NotNull(response);
        Assert.Equal(1000m, response.Amount);
    }

    [Fact]
    public async Task CreatePaymentAsync_ShouldSupportPartialPayments()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var context = await CreateContextAsync(connection);

        var (userId, customerId) =
            await SeedCustomerAsync(context, "customer@test.com");
        var order = await SeedOrderAsync(context, customerId, 1000m);

        var service = CreateService(context);

        var first = await service.CreatePaymentAsync(
            userId,
            false,
            order.Id,
            new CreatePaymentRequest
            {
                Method = PaymentMethod.Card,
                Amount = 400m
            });
        var second = await service.CreatePaymentAsync(
            userId,
            false,
            order.Id,
            new CreatePaymentRequest
            {
                Method = PaymentMethod.Card,
                Amount = 600m
            });

        Assert.NotNull(first);
        Assert.NotNull(second);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreatePaymentAsync(
                userId,
                false,
                order.Id,
                new CreatePaymentRequest
                {
                    Method = PaymentMethod.Card,
                    Amount = 0.01m
                }));
    }

    [Fact]
    public async Task CreatePaymentAsync_ShouldRejectWhenFullyPaid()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var context = await CreateContextAsync(connection);

        var (userId, customerId) =
            await SeedCustomerAsync(context, "customer@test.com");
        var (staffUserId, _) =
            await SeedCustomerAsync(context, "staff@test.com");
        var order = await SeedOrderAsync(context, customerId, 1000m);

        var service = CreateService(context);

        var payment = await service.CreatePaymentAsync(
            userId,
            false,
            order.Id,
            new CreatePaymentRequest
            {
                Method = PaymentMethod.Card,
                Amount = 1000m
            });
        await service.UpdatePaymentStatusAsync(
            staffUserId,
            true,
            order.Id,
            payment!.Id,
            new UpdatePaymentStatusRequest
            {
                Status = PaymentStatus.Completed
            });

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreatePaymentAsync(
                userId,
                false,
                order.Id,
                new CreatePaymentRequest
                {
                    Method = PaymentMethod.Card,
                    Amount = 10m
                }));
    }

    [Fact]
    public async Task UpdatePaymentStatusAsync_ShouldSupportRefund()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var context = await CreateContextAsync(connection);

        var (customerUserId, customerId) =
            await SeedCustomerAsync(context, "customer@test.com");
        var (staffUserId, _) =
            await SeedCustomerAsync(context, "staff@test.com");
        var order = await SeedOrderAsync(context, customerId, 1000m);

        var service = CreateService(context);

        var payment = await service.CreatePaymentAsync(
            customerUserId,
            false,
            order.Id,
            new CreatePaymentRequest
            {
                Method = PaymentMethod.Card,
                Amount = 1000m
            });
        await service.UpdatePaymentStatusAsync(
            staffUserId,
            true,
            order.Id,
            payment!.Id,
            new UpdatePaymentStatusRequest
            {
                Status = PaymentStatus.Completed
            });

        var refunded = await service.UpdatePaymentStatusAsync(
            staffUserId,
            true,
            order.Id,
            payment.Id,
            new UpdatePaymentStatusRequest
            {
                Status = PaymentStatus.Refunded
            });

        Assert.NotNull(refunded);
        Assert.Equal(PaymentStatus.Refunded, refunded.Status);
    }

    [Fact]
    public async Task UpdatePaymentStatusAsync_ShouldRejectInvalidTransitions()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var context = await CreateContextAsync(connection);

        var (customerUserId, customerId) =
            await SeedCustomerAsync(context, "customer@test.com");
        var (staffUserId, _) =
            await SeedCustomerAsync(context, "staff@test.com");
        var order = await SeedOrderAsync(context, customerId, 1000m);

        var service = CreateService(context);

        var payment = await service.CreatePaymentAsync(
            customerUserId,
            false,
            order.Id,
            new CreatePaymentRequest
            {
                Method = PaymentMethod.Card,
                Amount = 1000m
            });

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UpdatePaymentStatusAsync(
                staffUserId,
                true,
                order.Id,
                payment!.Id,
                new UpdatePaymentStatusRequest
                {
                    Status = PaymentStatus.Refunded
                }));
    }

    [Fact]
    public async Task UpdatePaymentStatusAsync_ShouldRejectNonStaffCaller()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var context = await CreateContextAsync(connection);

        var (customerUserId, customerId) =
            await SeedCustomerAsync(context, "customer@test.com");
        var order = await SeedOrderAsync(context, customerId, 1000m);

        var payment = await CreateService(context).CreatePaymentAsync(
            customerUserId,
            false,
            order.Id,
            new CreatePaymentRequest
            {
                Method = PaymentMethod.Card,
                Amount = 1000m
            });

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            CreateService(context).UpdatePaymentStatusAsync(
                customerUserId,
                false,
                order.Id,
                payment!.Id,
                new UpdatePaymentStatusRequest
                {
                    Status = PaymentStatus.Completed
                }));
    }

    [Fact]
    public async Task GetPaymentsAsync_ShouldReturnOwnOrderPayments()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var context = await CreateContextAsync(connection);

        var (userId, customerId) =
            await SeedCustomerAsync(context, "customer@test.com");
        var (_, customerIdB) =
            await SeedCustomerAsync(context, "b@test.com");
        var order = await SeedOrderAsync(context, customerId, 1000m);

        var service = CreateService(context);

        await service.CreatePaymentAsync(
            userId,
            false,
            order.Id,
            new CreatePaymentRequest
            {
                Method = PaymentMethod.Card,
                Amount = 500m
            });

        var payments = await service.GetPaymentsAsync(
            userId,
            false,
            order.Id);

        Assert.NotNull(payments);
        Assert.Single(payments);
        Assert.Equal(500m, payments[0].Amount);

        var other = await service.GetPaymentsAsync(
            customerIdB,
            false,
            order.Id);

        Assert.Null(other);
    }
}
