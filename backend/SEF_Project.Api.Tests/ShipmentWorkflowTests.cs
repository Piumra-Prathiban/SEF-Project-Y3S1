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

public class ShipmentWorkflowTests
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
    public async Task CreateShipmentAsync_ShouldCreatePendingShipment()
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
            OrderStatus.Confirmed,
            1000m);

        var response = await CreateService(context).CreateShipmentAsync(
            staffUserId,
            true,
            order.Id,
            new CreateShipmentRequest
            {
                Carrier = "FastCourier",
                TrackingNumber = "TRK-123"
            });

        Assert.NotNull(response);
        Assert.Equal(ShipmentStatus.Pending, response.Status);
        Assert.Equal("FastCourier", response.Carrier);
        Assert.Equal("TRK-123", response.TrackingNumber);
    }

    [Fact]
    public async Task GetShipmentsAsync_ShouldReturnOwnOrderShipments()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var context = await CreateContextAsync(connection);

        var (customerUserId, customerId) =
            await SeedCustomerAsync(context, "customer@test.com");
        var (staffUserId, _) =
            await SeedCustomerAsync(context, "staff@test.com");
        var (_, customerIdB) =
            await SeedCustomerAsync(context, "b@test.com");
        var order = await SeedOrderAsync(
            context,
            customerId,
            OrderStatus.Confirmed,
            1000m);

        var service = CreateService(context);

        await service.CreateShipmentAsync(
            staffUserId,
            true,
            order.Id,
            new CreateShipmentRequest { Carrier = "FastCourier" });

        var shipments = await service.GetShipmentsAsync(
            customerUserId,
            false,
            order.Id);

        Assert.NotNull(shipments);
        Assert.Single(shipments);

        var other = await service.GetShipmentsAsync(
            customerIdB,
            false,
            order.Id);

        Assert.Null(other);
    }

    [Fact]
    public async Task CreateShipmentAsync_ShouldReturnNull_ForInvalidOrder()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var context = await CreateContextAsync(connection);

        var (staffUserId, _) =
            await SeedCustomerAsync(context, "staff@test.com");

        var response = await CreateService(context).CreateShipmentAsync(
            staffUserId,
            true,
            Guid.NewGuid(),
            new CreateShipmentRequest());

        Assert.Null(response);
    }

    [Fact]
    public async Task CreateShipmentAsync_ShouldRejectDuplicateShipment()
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
            OrderStatus.Confirmed,
            1000m);

        var service = CreateService(context);

        await service.CreateShipmentAsync(
            staffUserId,
            true,
            order.Id,
            new CreateShipmentRequest());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateShipmentAsync(
                staffUserId,
                true,
                order.Id,
                new CreateShipmentRequest()));
    }

    [Fact]
    public async Task ShipmentOperations_ShouldRejectNonStaffCallers()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var context = await CreateContextAsync(connection);

        var (customerUserId, customerId) =
            await SeedCustomerAsync(context, "customer@test.com");
        var (staffUserId, _) =
            await SeedCustomerAsync(context, "staff@test.com");
        var order = await SeedOrderAsync(
            context,
            customerId,
            OrderStatus.Confirmed,
            1000m);

        var service = CreateService(context);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.CreateShipmentAsync(
                customerUserId,
                false,
                order.Id,
                new CreateShipmentRequest()));

        var shipment = await service.CreateShipmentAsync(
            staffUserId,
            true,
            order.Id,
            new CreateShipmentRequest());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.UpdateShipmentStatusAsync(
                customerUserId,
                false,
                order.Id,
                shipment!.Id,
                new UpdateShipmentRequest
                {
                    Status = ShipmentStatus.Shipped
                }));
    }

    [Fact]
    public async Task UpdateShipmentStatusAsync_ShouldCompleteFulfilment()
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
            OrderStatus.Ready,
            1000m);

        var service = CreateService(context);

        var shipment = await service.CreateShipmentAsync(
            staffUserId,
            true,
            order.Id,
            new CreateShipmentRequest());

        var shipped = await service.UpdateShipmentStatusAsync(
            staffUserId,
            true,
            order.Id,
            shipment!.Id,
            new UpdateShipmentRequest
            {
                Status = ShipmentStatus.Shipped
            });

        Assert.NotNull(shipped);
        Assert.Equal(ShipmentStatus.Shipped, shipped.Status);
        Assert.NotNull(shipped.ShippedAt);

        var delivered = await service.UpdateShipmentStatusAsync(
            staffUserId,
            true,
            order.Id,
            shipment.Id,
            new UpdateShipmentRequest
            {
                Status = ShipmentStatus.Delivered
            });

        Assert.NotNull(delivered);
        Assert.Equal(ShipmentStatus.Delivered, delivered.Status);
        Assert.NotNull(delivered.DeliveredAt);

        var persistedOrder = await context.Orders.AsNoTracking()
            .SingleAsync();
        Assert.Equal(OrderStatus.Completed, persistedOrder.Status);

        var history = await context.OrderStatusHistory.AsNoTracking()
            .SingleAsync(h => h.Status == OrderStatus.Completed);
        Assert.Equal(staffUserId, history.ChangedByUserId);
    }

    [Fact]
    public async Task UpdateShipmentStatusAsync_ShouldRejectInvalidTransitions()
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
            OrderStatus.Ready,
            1000m);

        var service = CreateService(context);

        var shipment = await service.CreateShipmentAsync(
            staffUserId,
            true,
            order.Id,
            new CreateShipmentRequest());

        await service.UpdateShipmentStatusAsync(
            staffUserId,
            true,
            order.Id,
            shipment!.Id,
            new UpdateShipmentRequest
            {
                Status = ShipmentStatus.Shipped
            });

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UpdateShipmentStatusAsync(
                staffUserId,
                true,
                order.Id,
                shipment.Id,
                new UpdateShipmentRequest
                {
                    Status = ShipmentStatus.Pending
                }));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UpdateShipmentStatusAsync(
                staffUserId,
                true,
                order.Id,
                shipment.Id,
                new UpdateShipmentRequest
                {
                    Status = ShipmentStatus.Shipped
                }));
    }

    [Fact]
    public async Task UpdateShipmentStatusAsync_ShouldRequireReadyOrder()
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
            OrderStatus.Preparing,
            1000m);

        var service = CreateService(context);

        var shipment = await service.CreateShipmentAsync(
            staffUserId,
            true,
            order.Id,
            new CreateShipmentRequest());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UpdateShipmentStatusAsync(
                staffUserId,
                true,
                order.Id,
                shipment!.Id,
                new UpdateShipmentRequest
                {
                    Status = ShipmentStatus.Shipped
                }));

        var persistedOrder = await context.Orders.AsNoTracking()
            .SingleAsync();
        Assert.Equal(OrderStatus.Preparing, persistedOrder.Status);
    }
}
