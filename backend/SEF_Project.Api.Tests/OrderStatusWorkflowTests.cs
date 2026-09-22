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

public class OrderStatusWorkflowTests
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
        OrderStatus status,
        decimal total)
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
    public async Task UpdateOrderStatusAsync_ShouldApplyValidTransitions()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var context = await CreateContextAsync(connection);

        var (_, customerId) =
            await SeedCustomerAsync(context, "customer@test.com");
        var (staffUserId, _) =
            await SeedCustomerAsync(context, "staff@test.com");
        var order = await SeedOrderAsync(
            context,
            customerId,
            OrderStatus.Pending,
            100m);

        var service = CreateService(context);

        var confirmed = await service.UpdateOrderStatusAsync(
            staffUserId,
            true,
            order.Id,
            new UpdateOrderStatusRequest
            {
                Status = OrderStatus.Confirmed
            });

        Assert.NotNull(confirmed);
        Assert.Equal(OrderStatus.Confirmed, confirmed.Status);

        var preparing = await service.UpdateOrderStatusAsync(
            staffUserId,
            true,
            order.Id,
            new UpdateOrderStatusRequest
            {
                Status = OrderStatus.Preparing
            });

        Assert.NotNull(preparing);
        Assert.Equal(OrderStatus.Preparing, preparing.Status);

        var persisted = await context.Orders.AsNoTracking()
            .SingleAsync();
        Assert.Equal(OrderStatus.Preparing, persisted.Status);
    }

    [Fact]
    public async Task UpdateOrderStatusAsync_ShouldRejectInvalidTransition()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var context = await CreateContextAsync(connection);

        var (_, customerId) =
            await SeedCustomerAsync(context, "customer@test.com");
        var (staffUserId, _) =
            await SeedCustomerAsync(context, "staff@test.com");
        var order = await SeedOrderAsync(
            context,
            customerId,
            OrderStatus.Completed,
            100m);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateService(context).UpdateOrderStatusAsync(
                staffUserId,
                true,
                order.Id,
                new UpdateOrderStatusRequest
                {
                    Status = OrderStatus.Preparing
                }));
    }

    [Fact]
    public async Task UpdateOrderStatusAsync_ShouldRejectSameStatus()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var context = await CreateContextAsync(connection);

        var (_, customerId) =
            await SeedCustomerAsync(context, "customer@test.com");
        var (staffUserId, _) =
            await SeedCustomerAsync(context, "staff@test.com");
        var order = await SeedOrderAsync(
            context,
            customerId,
            OrderStatus.Pending,
            100m);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateService(context).UpdateOrderStatusAsync(
                staffUserId,
                true,
                order.Id,
                new UpdateOrderStatusRequest
                {
                    Status = OrderStatus.Pending
                }));
    }

    [Fact]
    public async Task UpdateOrderStatusAsync_ShouldRejectNonStaffCaller()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var context = await CreateContextAsync(connection);

        var (customerUserId, customerId) =
            await SeedCustomerAsync(context, "customer@test.com");
        var order = await SeedOrderAsync(
            context,
            customerId,
            OrderStatus.Pending,
            100m);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            CreateService(context).UpdateOrderStatusAsync(
                customerUserId,
                false,
                order.Id,
                new UpdateOrderStatusRequest
                {
                    Status = OrderStatus.Confirmed
                }));

        var persisted = await context.Orders.AsNoTracking()
            .SingleAsync();
        Assert.Equal(OrderStatus.Pending, persisted.Status);
    }

    [Fact]
    public async Task UpdateOrderStatusAsync_ShouldRecordStatusHistory()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var context = await CreateContextAsync(connection);

        var (_, customerId) =
            await SeedCustomerAsync(context, "customer@test.com");
        var (staffUserId, _) =
            await SeedCustomerAsync(context, "staff@test.com");
        var order = await SeedOrderAsync(
            context,
            customerId,
            OrderStatus.Pending,
            100m);

        await CreateService(context).UpdateOrderStatusAsync(
            staffUserId,
            true,
            order.Id,
            new UpdateOrderStatusRequest
            {
                Status = OrderStatus.Confirmed,
                Note = "Confirmed by staff."
            });

        var entry = await context.OrderStatusHistory.AsNoTracking()
            .SingleAsync(h => h.OrderId == order.Id);

        Assert.Equal(OrderStatus.Confirmed, entry.Status);
        Assert.Equal(staffUserId, entry.ChangedByUserId);
        Assert.Equal("Confirmed by staff.", entry.Note);
    }

    [Fact]
    public async Task UpdateOrderStatusAsync_FailedTransition_ShouldRollback()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var context = await CreateContextAsync(connection);

        var (_, customerId) =
            await SeedCustomerAsync(context, "customer@test.com");
        var (staffUserId, _) =
            await SeedCustomerAsync(context, "staff@test.com");
        var order = await SeedOrderAsync(
            context,
            customerId,
            OrderStatus.Completed,
            100m);

        var historyBefore = await context.OrderStatusHistory
            .CountAsync(h => h.OrderId == order.Id);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateService(context).UpdateOrderStatusAsync(
                staffUserId,
                true,
                order.Id,
                new UpdateOrderStatusRequest
                {
                    Status = OrderStatus.Preparing
                }));

        var persisted = await context.Orders.AsNoTracking()
            .SingleAsync(o => o.Id == order.Id);
        var historyAfter = await context.OrderStatusHistory
            .CountAsync(h => h.OrderId == order.Id);

        Assert.Equal(OrderStatus.Completed, persisted.Status);
        Assert.Equal(historyBefore, historyAfter);
    }

    [Fact]
    public async Task UpdateOrderStatusAsync_ShouldReturnNull_WhenOrderNotFound()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var context = await CreateContextAsync(connection);

        var (staffUserId, _) =
            await SeedCustomerAsync(context, "staff@test.com");

        var response = await CreateService(context)
            .UpdateOrderStatusAsync(
                staffUserId,
                true,
                Guid.NewGuid(),
                new UpdateOrderStatusRequest
                {
                    Status = OrderStatus.Confirmed
                });

        Assert.Null(response);
    }
}
