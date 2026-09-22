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

public class OrderServiceQueryTests
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
        decimal total,
        DateTime placedAt,
        string? orderNumber = null)
    {
        var order = new Order
        {
            OrderNumber = orderNumber ?? $"ORD-{Guid.NewGuid():N}",
            CustomerId = customerId,
            Status = status,
            Subtotal = total,
            Total = total,
            PlacedAt = placedAt
        };

        context.Orders.Add(order);
        await context.SaveChangesAsync();

        return order;
    }

    private static OrderService CreateService(AppDbContext context) =>
        new(context, NullLogger<OrderService>.Instance);

    [Fact]
    public async Task GetOrderByIdAsync_CustomerCanRetrieveOwnOrder()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var context = await CreateContextAsync(connection);

        var (userId, customerId) =
            await SeedCustomerAsync(context, "a@test.com");
        var order = await SeedOrderAsync(
            context,
            customerId,
            OrderStatus.Pending,
            1500m,
            DateTime.UtcNow);

        var response = await CreateService(context)
            .GetOrderByIdAsync(userId, false, order.Id);

        Assert.NotNull(response);
        Assert.Equal(order.Id, response.Id);
        Assert.Equal(order.OrderNumber, response.OrderNumber);
    }

    [Fact]
    public async Task GetOrderByIdAsync_CustomerCannotRetrieveOtherCustomersOrder()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var context = await CreateContextAsync(connection);

        var (_, customerIdA) =
            await SeedCustomerAsync(context, "a@test.com");
        var (userIdB, _) =
            await SeedCustomerAsync(context, "b@test.com");
        var order = await SeedOrderAsync(
            context,
            customerIdA,
            OrderStatus.Pending,
            100m,
            DateTime.UtcNow);

        var service = CreateService(context);

        Assert.Null(await service.GetOrderByIdAsync(userIdB, false, order.Id));

        var list = await service.GetOrdersAsync(
            userIdB,
            false,
            new OrderQuery());
        Assert.Empty(list.Items);
        Assert.Equal(0, list.TotalCount);
    }

    [Fact]
    public async Task GetOrderByIdAsync_StaffCanRetrieveAnyOrder()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var context = await CreateContextAsync(connection);

        var (_, customerIdA) =
            await SeedCustomerAsync(context, "a@test.com");
        var (userIdStaff, _) =
            await SeedCustomerAsync(context, "staff@test.com");
        var order = await SeedOrderAsync(
            context,
            customerIdA,
            OrderStatus.Pending,
            100m,
            DateTime.UtcNow);

        var response = await CreateService(context)
            .GetOrderByIdAsync(userIdStaff, true, order.Id);

        Assert.NotNull(response);
        Assert.Equal(order.Id, response.Id);
    }

    [Fact]
    public async Task GetOrderByIdAsync_ReturnsNull_WhenOrderNotFound()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var context = await CreateContextAsync(connection);

        var (userId, _) =
            await SeedCustomerAsync(context, "a@test.com");

        var response = await CreateService(context)
            .GetOrderByIdAsync(userId, false, Guid.NewGuid());

        Assert.Null(response);
    }

    [Fact]
    public async Task GetOrdersAsync_ShouldPaginate()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var context = await CreateContextAsync(connection);

        var (userId, customerId) =
            await SeedCustomerAsync(context, "a@test.com");
        var baseTime = DateTime.UtcNow;

        for (var i = 0; i < 5; i++)
        {
            await SeedOrderAsync(
                context,
                customerId,
                OrderStatus.Pending,
                100m + i,
                baseTime.AddMinutes(i));
        }

        var service = CreateService(context);

        var page = await service.GetOrdersAsync(
            userId,
            false,
            new OrderQuery { Page = 2, PageSize = 2 });

        Assert.Equal(5, page.TotalCount);
        Assert.Equal(2, page.Items.Count);
        Assert.Equal(2, page.Page);
        Assert.Equal(2, page.PageSize);
    }

    [Fact]
    public async Task GetOrdersAsync_ShouldFilter_ByStatusDateAndOrderNumber()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var context = await CreateContextAsync(connection);

        var (userId, customerId) =
            await SeedCustomerAsync(context, "a@test.com");
        var baseTime = DateTime.UtcNow;

        await SeedOrderAsync(
            context,
            customerId,
            OrderStatus.Pending,
            100m,
            baseTime,
            "ORD-LOOKUP-001");
        await SeedOrderAsync(
            context,
            customerId,
            OrderStatus.Completed,
            200m,
            baseTime.AddDays(1));
        await SeedOrderAsync(
            context,
            customerId,
            OrderStatus.Pending,
            300m,
            baseTime.AddDays(2),
            "ORD-LOOKUP-002");

        var service = CreateService(context);

        var byStatus = await service.GetOrdersAsync(
            userId,
            false,
            new OrderQuery
            {
                Status = OrderStatus.Pending,
                PageSize = 50
            });

        Assert.Equal(2, byStatus.TotalCount);
        Assert.All(
            byStatus.Items,
            item => Assert.Equal(OrderStatus.Pending, item.Status));

        var byDate = await service.GetOrdersAsync(
            userId,
            false,
            new OrderQuery
            {
                From = baseTime.AddDays(1),
                PageSize = 50
            });

        Assert.Equal(2, byDate.TotalCount);

        var byNumber = await service.GetOrdersAsync(
            userId,
            false,
            new OrderQuery { OrderNumber = "ORD-LOOKUP", PageSize = 50 });

        Assert.Equal(2, byNumber.TotalCount);
    }

    [Fact]
    public async Task GetOrdersAsync_ShouldSortByTotal()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var context = await CreateContextAsync(connection);

        var (userId, customerId) =
            await SeedCustomerAsync(context, "a@test.com");
        var baseTime = DateTime.UtcNow;

        await SeedOrderAsync(
            context,
            customerId,
            OrderStatus.Pending,
            100m,
            baseTime);
        await SeedOrderAsync(
            context,
            customerId,
            OrderStatus.Pending,
            300m,
            baseTime.AddMinutes(1));
        await SeedOrderAsync(
            context,
            customerId,
            OrderStatus.Pending,
            200m,
            baseTime.AddMinutes(2));

        var service = CreateService(context);

        var ascending = await service.GetOrdersAsync(
            userId,
            false,
            new OrderQuery
            {
                SortBy = "total",
                SortDirection = "asc",
                PageSize = 50
            });

        Assert.Equal(
            new[] { 100m, 200m, 300m },
            ascending.Items.Select(i => i.Total).ToArray());

        var descending = await service.GetOrdersAsync(
            userId,
            false,
            new OrderQuery
            {
                SortBy = "total",
                SortDirection = "desc",
                PageSize = 50
            });

        Assert.Equal(
            new[] { 300m, 200m, 100m },
            descending.Items.Select(i => i.Total).ToArray());
    }

    [Fact]
    public async Task GetOrderStatusHistoryAsync_ReturnsOwnOrderHistory()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var context = await CreateContextAsync(connection);

        var (userId, customerId) =
            await SeedCustomerAsync(context, "a@test.com");
        var (userIdB, _) =
            await SeedCustomerAsync(context, "b@test.com");

        var order = await SeedOrderAsync(
            context,
            customerId,
            OrderStatus.Pending,
            100m,
            DateTime.UtcNow);

        var created = DateTime.UtcNow;

        context.OrderStatusHistory.Add(new OrderStatusHistory
        {
            OrderId = order.Id,
            Status = OrderStatus.Pending,
            ChangedAt = created
        });
        context.OrderStatusHistory.Add(new OrderStatusHistory
        {
            OrderId = order.Id,
            Status = OrderStatus.Confirmed,
            ChangedAt = created.AddMinutes(1)
        });
        await context.SaveChangesAsync();

        var service = CreateService(context);

        var history = await service.GetOrderStatusHistoryAsync(
            userId,
            false,
            order.Id);

        Assert.NotNull(history);
        Assert.Equal(2, history.Count);
        Assert.Equal(OrderStatus.Pending, history[0].Status);
        Assert.Equal(OrderStatus.Confirmed, history[1].Status);

        Assert.Null(await service.GetOrderStatusHistoryAsync(
            userIdB,
            false,
            order.Id));
    }
}
