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

public class OrderCheckoutEdgeCaseTests
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

    private static async Task<int> SeedCustomerAsync(
        AppDbContext context,
        string email,
        bool isActive = true)
    {
        var user = new User
        {
            Email = email,
            PasswordHash = "not-a-real-hash",
            FirstName = "Test",
            LastName = "Customer",
            RoleId = 1,
            IsActive = isActive
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
            PostalCode = "00100"
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

    private static OrderService CreateService(AppDbContext context) =>
        new(context, NullLogger<OrderService>.Instance);

    [Fact]
    public async Task CreateOrderAsync_ShouldRejectUserWithoutCustomerProfile()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var context = await CreateContextAsync(connection);

        var user = new User
        {
            Email = "orphan@test.com",
            PasswordHash = "not-a-real-hash",
            FirstName = "No",
            LastName = "Profile",
            RoleId = 1,
            IsActive = true
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var variant = await context.ProductVariants
            .FirstAsync(v => v.Sku == MargheritaSmallSku);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            CreateService(context).CreateOrderAsync(
                user.Id,
                OrderRequest(variant.Id, 1)));
    }

    [Fact]
    public async Task CreateOrderAsync_ShouldRejectInactiveCustomer()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var context = await CreateContextAsync(connection);

        var userId = await SeedCustomerAsync(
            context,
            "inactive@test.com",
            isActive: false);
        var variant = await context.ProductVariants
            .FirstAsync(v => v.Sku == MargheritaSmallSku);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            CreateService(context).CreateOrderAsync(
                userId,
                OrderRequest(variant.Id, 1)));
    }

    [Fact]
    public async Task CreateOrderAsync_ShouldRejectInactiveVariant()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var context = await CreateContextAsync(connection);

        var userId = await SeedCustomerAsync(context, "customer@test.com");
        var variant = await context.ProductVariants
            .FirstAsync(v => v.Sku == MargheritaSmallSku);

        variant.IsActive = false;
        await context.SaveChangesAsync();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            CreateService(context).CreateOrderAsync(
                userId,
                OrderRequest(variant.Id, 1)));
    }

    [Fact]
    public async Task CreateOrderAsync_ShouldMergeDuplicateVariantLines()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var context = await CreateContextAsync(connection);

        var userId = await SeedCustomerAsync(context, "customer@test.com");
        var variant = await context.ProductVariants
            .FirstAsync(v => v.Sku == MargheritaSmallSku);

        var request = OrderRequest(variant.Id, 2);
        request.Items.Add(new CreateOrderItemRequest
        {
            ProductVariantId = variant.Id,
            Quantity = 3
        });

        var response = await CreateService(context).CreateOrderAsync(
            userId,
            request);

        var item = Assert.Single(response.Items);
        Assert.Equal(5, item.Quantity);
        Assert.Equal(Math.Round(variant.Price * 5, 2), item.LineTotal);

        var persisted = await context.OrderItems.AsNoTracking()
            .ToListAsync();
        Assert.Single(persisted);
    }

    [Fact]
    public async Task CreateOrderAsync_ShouldSnapshotDeliveryAddress()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var context = await CreateContextAsync(connection);

        var userId = await SeedCustomerAsync(context, "customer@test.com");
        var variant = await context.ProductVariants
            .FirstAsync(v => v.Sku == MargheritaSmallSku);

        var request = new CreateOrderRequest
        {
            Items = new List<CreateOrderItemRequest>
            {
                new() { ProductVariantId = variant.Id, Quantity = 1 }
            },
            DeliveryAddress = new CreateOrderAddressRequest
            {
                FullName = "Chamathka Silva",
                Line1 = "42 Galle Road",
                Line2 = "Apartment 3B",
                City = "Mount Lavinia",
                Province = "Western",
                PostalCode = "10370",
                Country = "Sri Lanka",
                Phone = "+94 71 222 3333"
            },
            PaymentMethod = PaymentMethod.Card
        };

        await CreateService(context).CreateOrderAsync(userId, request);

        var address = await context.OrderAddresses.AsNoTracking()
            .SingleAsync();

        Assert.Equal("Chamathka Silva", address.FullName);
        Assert.Equal("42 Galle Road", address.Line1);
        Assert.Equal("Apartment 3B", address.Line2);
        Assert.Equal("Mount Lavinia", address.City);
        Assert.Equal("Western", address.Province);
        Assert.Equal("10370", address.PostalCode);
        Assert.Equal("Sri Lanka", address.Country);
        Assert.Equal("+94 71 222 3333", address.Phone);
    }

    [Fact]
    public async Task CreatePaymentAsync_ShouldRejectCancelledOrder()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var context = await CreateContextAsync(connection);

        var (userId, customerId) =
            await SeedCustomerWithIdAsync(context, "customer@test.com");

        var order = new Order
        {
            OrderNumber = $"ORD-{Guid.NewGuid():N}",
            CustomerId = customerId,
            Status = OrderStatus.Cancelled,
            Subtotal = 100m,
            Total = 100m,
            PlacedAt = DateTime.UtcNow
        };
        context.Orders.Add(order);
        await context.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateService(context).CreatePaymentAsync(
                userId,
                false,
                order.Id,
                new CreatePaymentRequest
                {
                    Method = PaymentMethod.Card,
                    Amount = 50m
                }));
    }

    private static async Task<(int UserId, int CustomerId)>
        SeedCustomerWithIdAsync(
            AppDbContext context,
            string email)
    {
        var userId = await SeedCustomerAsync(context, email);
        var customer = await context.Customers
            .SingleAsync(c => c.UserId == userId);

        return (userId, customer.Id);
    }
}
