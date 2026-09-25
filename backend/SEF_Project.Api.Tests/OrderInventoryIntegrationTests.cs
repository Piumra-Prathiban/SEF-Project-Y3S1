using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SEF_Project.Api.Data;
using SEF_Project.Api.DTOs.Orders;
using SEF_Project.Api.Models;
using SEF_Project.Api.Models.Enums;
using SEF_Project.Api.Services.Orders;

namespace SEF_Project.Api.Tests;

public class OrderInventoryIntegrationTests
{
    private const string TShirtXsSku = "TSH-CLS-XS";

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

    private static async Task MoveOrderToAsync(
        OrderService service,
        int staffUserId,
        Guid orderId,
        OrderStatus target)
    {
        foreach (var status in new[]
                 {
                     OrderStatus.Confirmed,
                     OrderStatus.Preparing,
                     OrderStatus.Ready,
                     OrderStatus.Completed
                 })
        {
            await service.UpdateOrderStatusAsync(
                staffUserId,
                true,
                orderId,
                new UpdateOrderStatusRequest { Status = status });

            if (status == target)
            {
                return;
            }
        }
    }

    [Fact]
    public async Task CheckoutToDelivery_ShouldReserveThenConsumeStock()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var context = await CreateContextAsync(connection);

        var (customerUserId, _) =
            await SeedCustomerAsync(context, "customer@test.com");
        var (staffUserId, _) =
            await SeedCustomerAsync(context, "staff@test.com");
        var variant = await context.ProductVariants
            .FirstAsync(v => v.Sku == TShirtXsSku);
        var onHandBefore = (await context.Inventory
            .SingleAsync(i => i.ProductVariantId == variant.Id))
            .QuantityOnHand;

        var service = CreateService(context);

        var order = await PlaceOrderAsync(
            service,
            customerUserId,
            variant.Id,
            2);

        var afterCheckout = await context.Inventory.AsNoTracking()
            .SingleAsync(i => i.ProductVariantId == variant.Id);
        Assert.Equal(2, afterCheckout.ReservedQuantity);
        Assert.Equal(onHandBefore, afterCheckout.QuantityOnHand);

        await MoveOrderToAsync(service, staffUserId, order.Id, OrderStatus.Ready);

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
            new UpdateShipmentRequest { Status = ShipmentStatus.Shipped });
        await service.UpdateShipmentStatusAsync(
            staffUserId,
            true,
            order.Id,
            shipment.Id,
            new UpdateShipmentRequest { Status = ShipmentStatus.Delivered });

        var persistedOrder = await context.Orders.AsNoTracking()
            .SingleAsync();
        Assert.Equal(OrderStatus.Completed, persistedOrder.Status);

        var afterDelivery = await context.Inventory.AsNoTracking()
            .SingleAsync(i => i.ProductVariantId == variant.Id);
        Assert.Equal(0, afterDelivery.ReservedQuantity);
        Assert.Equal(onHandBefore - 2, afterDelivery.QuantityOnHand);

        var sale = await context.InventoryTransactions.AsNoTracking()
            .SingleAsync(t => t.Type == InventoryTransactionType.Sale);
        Assert.Equal(-2, sale.QuantityChange);
        Assert.Equal(afterDelivery.QuantityOnHand, sale.QuantityOnHandAfter);
    }

    [Fact]
    public async Task CompletingOrderViaStatus_ShouldConsumeStock()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var context = await CreateContextAsync(connection);

        var (customerUserId, _) =
            await SeedCustomerAsync(context, "customer@test.com");
        var (staffUserId, _) =
            await SeedCustomerAsync(context, "staff@test.com");
        var variant = await context.ProductVariants
            .FirstAsync(v => v.Sku == TShirtXsSku);
        var onHandBefore = (await context.Inventory
            .SingleAsync(i => i.ProductVariantId == variant.Id))
            .QuantityOnHand;

        var service = CreateService(context);

        var order = await PlaceOrderAsync(
            service,
            customerUserId,
            variant.Id,
            3);

        await MoveOrderToAsync(
            service,
            staffUserId,
            order.Id,
            OrderStatus.Completed);

        var inventory = await context.Inventory.AsNoTracking()
            .SingleAsync(i => i.ProductVariantId == variant.Id);
        Assert.Equal(0, inventory.ReservedQuantity);
        Assert.Equal(onHandBefore - 3, inventory.QuantityOnHand);

        var sale = await context.InventoryTransactions.AsNoTracking()
            .SingleAsync(t => t.Type == InventoryTransactionType.Sale);
        Assert.Equal(-3, sale.QuantityChange);
    }

    [Fact]
    public async Task Cancellation_ShouldReleaseWithoutConsumingStock()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var context = await CreateContextAsync(connection);

        var (customerUserId, _) =
            await SeedCustomerAsync(context, "customer@test.com");
        var variant = await context.ProductVariants
            .FirstAsync(v => v.Sku == TShirtXsSku);
        var onHandBefore = (await context.Inventory
            .SingleAsync(i => i.ProductVariantId == variant.Id))
            .QuantityOnHand;

        var service = CreateService(context);

        var order = await PlaceOrderAsync(
            service,
            customerUserId,
            variant.Id,
            2);

        await service.CancelOrderAsync(
            customerUserId,
            false,
            order.Id,
            new CancelOrderRequest());

        var inventory = await context.Inventory.AsNoTracking()
            .SingleAsync(i => i.ProductVariantId == variant.Id);
        Assert.Equal(0, inventory.ReservedQuantity);
        Assert.Equal(onHandBefore, inventory.QuantityOnHand);

        var saleCount = await context.InventoryTransactions.AsNoTracking()
            .CountAsync(t => t.Type == InventoryTransactionType.Sale);
        Assert.Equal(0, saleCount);
    }
}
