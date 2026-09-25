using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SEF_Project.Api.Data;
using SEF_Project.Api.DTOs.Orders;
using SEF_Project.Api.DTOs.Shopping;
using SEF_Project.Api.Models;
using SEF_Project.Api.Models.Enums;
using SEF_Project.Api.Services.Orders;
using SEF_Project.Api.Services.Shopping;

namespace SEF_Project.Api.Tests;

public class CrossComponentIntegrationTests
{
    [Fact]
    public async Task CartToCheckoutHandoff_ShouldUseAuthoritativeCatalogueData()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var user = new User
        {
            Email = "integration-customer@test.com",
            PasswordHash = "not-a-real-hash",
            FirstName = "Integration",
            LastName = "Customer",
            RoleId = 1,
            IsActive = true
        };
        context.Customers.Add(new Customer { User = user });
        await context.SaveChangesAsync();

        var variant = await context.ProductVariants
            .SingleAsync(item => item.Sku == "TSH-CLS-XS");
        var inventory = await context.Inventory
            .SingleAsync(item => item.ProductVariantId == variant.Id);
        var reservedBefore = inventory.ReservedQuantity;

        var cartService = new CartService(context);
        var cartResult = await cartService.AddItemAsync(
            user.Id,
            new AddCartItemRequest
            {
                ProductVariantId = variant.Id,
                Quantity = 2
            });
        var cart = Assert.IsType<CartResponse>(cartResult.Cart);

        // A price change after the cart was read must be picked up by checkout.
        // Only variant identifiers and quantities cross the component boundary.
        variant.Price += 125m;
        await context.SaveChangesAsync();

        var request = new CreateOrderRequest
        {
            Items = cart.Items
                .Select(item => new CreateOrderItemRequest
                {
                    ProductVariantId = item.ProductVariantId,
                    Quantity = item.Quantity
                })
                .ToList(),
            DeliveryAddress = new CreateOrderAddressRequest
            {
                FullName = "Integration Customer",
                Line1 = "12 Main Street",
                City = "Colombo",
                PostalCode = "00100"
            },
            PaymentMethod = PaymentMethod.Card
        };

        var orderService = new OrderService(
            context,
            NullLogger<OrderService>.Instance);
        var order = await orderService.CreateOrderAsync(user.Id, request);

        var orderItem = Assert.Single(order.Items);
        Assert.Equal(variant.Id, orderItem.ProductVariantId);
        Assert.Equal(2, orderItem.Quantity);
        Assert.Equal(variant.Price, orderItem.UnitPrice);
        Assert.Equal(variant.Price * 2, order.Total);

        await context.Entry(inventory).ReloadAsync();
        Assert.Equal(reservedBefore + 2, inventory.ReservedQuantity);
    }
}
