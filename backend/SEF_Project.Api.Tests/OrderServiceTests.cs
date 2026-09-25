using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SEF_Project.Api.Data;
using SEF_Project.Api.DTOs.Orders;
using SEF_Project.Api.Models;
using SEF_Project.Api.Models.Catalog;
using SEF_Project.Api.Models.Enums;
using SEF_Project.Api.Services.Orders;

namespace SEF_Project.Api.Tests;

public class OrderServiceTests
{
    private const string TShirtXsSku = "TSH-CLS-XS";

    private static async Task<(SqliteConnection Connection, AppDbContext Context)>
        CreateContextAsync()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();

        return (connection, context);
    }

    private static async Task<int> SeedCustomerAsync(AppDbContext context)
    {
        var user = new User
        {
            Email = "customer@test.com",
            PasswordHash = "not-a-real-hash",
            FirstName = "Test",
            LastName = "Customer",
            RoleId = 1,
            IsActive = true
        };

        context.Users.Add(user);
        context.Customers.Add(new Customer { User = user });

        await context.SaveChangesAsync();

        return user.Id;
    }

    private static CreateOrderAddressRequest ValidAddress() =>
        new()
        {
            FullName = "Test Customer",
            Line1 = "12 Main Street",
            City = "Colombo",
            PostalCode = "00100",
            Phone = "+94 77 123 4567"
        };

    private static CreateOrderRequest OrderRequest(
        Guid variantId,
        int quantity) =>
        new()
        {
            Items = new List<CreateOrderItemRequest>
            {
                new() { ProductVariantId = variantId, Quantity = quantity }
            },
            DeliveryAddress = ValidAddress(),
            PaymentMethod = PaymentMethod.Card
        };

    [Fact]
    public async Task CreateOrderAsync_ShouldCreateOrder_WithCorrectTotals()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var userId = await SeedCustomerAsync(context);
        var variant = await context.ProductVariants
            .FirstAsync(v => v.Sku == TShirtXsSku);

        var service = new OrderService(
            context,
            NullLogger<OrderService>.Instance);

        var response = await service.CreateOrderAsync(
            userId,
            OrderRequest(variant.Id, 2));

        var order = await context.Orders
            .Include(o => o.Items)
            .Include(o => o.DeliveryAddress)
            .SingleAsync();

        Assert.Equal(OrderStatus.Pending, order.Status);
        Assert.False(string.IsNullOrWhiteSpace(order.OrderNumber));
        Assert.Equal(5000m, order.Subtotal);
        Assert.Equal(5000m, order.Total);

        var item = Assert.Single(order.Items);
        Assert.Equal(variant.Id, item.ProductVariantId);
        Assert.Equal(2, item.Quantity);
        Assert.Equal(variant.Price, item.UnitPrice);
        Assert.Equal(5000m, item.LineTotal);

        Assert.NotNull(order.DeliveryAddress);
        Assert.Equal("Colombo", order.DeliveryAddress.City);
        Assert.Single(response.StatusHistory);
    }

    [Fact]
    public async Task CreateOrderAsync_ShouldReserveInventory_AndCreateTransaction()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var userId = await SeedCustomerAsync(context);
        var variant = await context.ProductVariants
            .FirstAsync(v => v.Sku == TShirtXsSku);
        var reservedBefore = (await context.Inventory
            .SingleAsync(i => i.ProductVariantId == variant.Id))
            .ReservedQuantity;

        var service = new OrderService(
            context,
            NullLogger<OrderService>.Instance);

        await service.CreateOrderAsync(
            userId,
            OrderRequest(variant.Id, 3));

        var inventoryAfter = await context.Inventory
            .SingleAsync(i => i.ProductVariantId == variant.Id);
        var transaction = await context.InventoryTransactions
            .SingleAsync();

        Assert.Equal(
            reservedBefore + 3,
            inventoryAfter.ReservedQuantity);
        Assert.Equal(
            InventoryTransactionType.Reservation,
            transaction.Type);
        Assert.Equal(-3, transaction.QuantityChange);
        Assert.Equal(
            inventoryAfter.QuantityOnHand,
            transaction.QuantityOnHandAfter);
    }

    [Fact]
    public async Task CreateOrderAsync_ShouldThrow_ForInvalidProductVariant()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var userId = await SeedCustomerAsync(context);
        var service = new OrderService(
            context,
            NullLogger<OrderService>.Instance);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateOrderAsync(
                userId,
                OrderRequest(Guid.NewGuid(), 1)));
    }

    [Fact]
    public async Task CreateOrderAsync_ShouldThrow_ForInvalidQuantity()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var userId = await SeedCustomerAsync(context);
        var variant = await context.ProductVariants
            .FirstAsync(v => v.Sku == TShirtXsSku);

        var service = new OrderService(
            context,
            NullLogger<OrderService>.Instance);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateOrderAsync(
                userId,
                OrderRequest(variant.Id, 0)));
    }

    [Fact]
    public async Task CreateOrderAsync_ShouldThrow_ForInsufficientInventory()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var userId = await SeedCustomerAsync(context);
        var variant = await context.ProductVariants
            .FirstAsync(v => v.Sku == TShirtXsSku);

        var service = new OrderService(
            context,
            NullLogger<OrderService>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateOrderAsync(
                userId,
                OrderRequest(variant.Id, 999999)));
    }

    [Fact]
    public async Task CreateOrderAsync_ShouldRollback_WithoutPartialState()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var userId = await SeedCustomerAsync(context);
        var variant = await context.ProductVariants
            .FirstAsync(v => v.Sku == TShirtXsSku);
        var reservedBefore = (await context.Inventory
            .SingleAsync(i => i.ProductVariantId == variant.Id))
            .ReservedQuantity;

        var service = new OrderService(
            context,
            NullLogger<OrderService>.Instance);

        var request = OrderRequest(variant.Id, 1);
        request.Items.Add(new CreateOrderItemRequest
        {
            ProductVariantId = Guid.NewGuid(),
            Quantity = 1
        });

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateOrderAsync(userId, request));

        Assert.Empty(await context.Orders.ToListAsync());
        Assert.Empty(await context.OrderItems.ToListAsync());
        Assert.Empty(await context.InventoryTransactions.ToListAsync());

        var inventoryAfter = await context.Inventory
            .SingleAsync(i => i.ProductVariantId == variant.Id);
        Assert.Equal(
            reservedBefore,
            inventoryAfter.ReservedQuantity);
    }

    [Fact]
    public async Task CreateOrderAsync_ShouldSnapshot_UnitPriceAtOrderTime()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var userId = await SeedCustomerAsync(context);
        var variant = await context.ProductVariants
            .FirstAsync(v => v.Sku == TShirtXsSku);
        var originalPrice = variant.Price;

        var service = new OrderService(
            context,
            NullLogger<OrderService>.Instance);

        var response = await service.CreateOrderAsync(
            userId,
            OrderRequest(variant.Id, 1));

        variant.Price = originalPrice + 500m;
        await context.SaveChangesAsync();

        var item = await context.OrderItems.SingleAsync();

        Assert.Equal(originalPrice, item.UnitPrice);
        Assert.Equal(originalPrice, response.Items[0].UnitPrice);
    }
}
