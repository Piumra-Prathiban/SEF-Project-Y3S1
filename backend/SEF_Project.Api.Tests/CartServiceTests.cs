using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SEF_Project.Api.Controllers;
using SEF_Project.Api.Data;
using SEF_Project.Api.Data.Configurations;
using SEF_Project.Api.DTOs.Shopping;
using SEF_Project.Api.Models;
using SEF_Project.Api.Models.Shopping;
using SEF_Project.Api.Services.Shopping;

namespace SEF_Project.Api.Tests;

public class CartServiceTests
{
    [Fact]
    public void CartController_RequiresAuthentication()
    {
        Assert.NotNull(typeof(CartController)
            .GetCustomAttribute<AuthorizeAttribute>());
    }

    [Fact]
    public async Task GetCart_ReturnsUnauthorizedWithoutUserClaim()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var context = await CreateContextAsync(connection);
        var controller = CreateController(new CartService(context));

        var response = await controller.GetCart(CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(response.Result);
    }

    [Fact]
    public void CartItemRequests_DoNotAcceptAuthoritativeFields()
    {
        var properties = typeof(AddCartItemRequest).GetProperties();
        var forbidden = new[]
        {
            "Price",
            "UnitPrice",
            "ProductName",
            "Stock",
            "Discount",
            "Subtotal",
            "Total",
            "LineTotal"
        };

        Assert.Contains(
            properties,
            property => property.Name == nameof(AddCartItemRequest.ProductVariantId));
        Assert.Contains(
            properties,
            property => property.Name == nameof(AddCartItemRequest.Quantity));

        foreach (var propertyName in forbidden)
        {
            Assert.DoesNotContain(
                properties,
                property => property.Name.Equals(
                    propertyName,
                    StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public async Task GetCart_ReturnsEmptyWithoutCreatingDatabaseCart()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var context = await CreateContextAsync(connection);
        var customer = await AddCustomerAsync(context, "empty-cart@test.com");

        var cart = await new CartService(context).GetCartAsync(customer.UserId);

        Assert.Null(cart.Id);
        Assert.Empty(cart.Items);
        Assert.Equal(0m, cart.Total);
        Assert.False(await context.Carts.AnyAsync(item =>
            item.CustomerId == customer.CustomerId));
    }

    [Fact]
    public async Task AddItem_CreatesCartUsingDatabaseVariantData()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var context = await CreateContextAsync(connection);
        var customer = await AddCustomerAsync(context, "add-cart@test.com");
        var controller = CreateController(
            new CartService(context),
            customer.UserId);

        var action = await controller.AddItem(
            new AddCartItemRequest
            {
                ProductVariantId = SeedData.VariantTShirtXs,
                Quantity = 2
            },
            CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(action.Result);
        var cart = Assert.IsType<CartResponse>(created.Value);
        var item = Assert.Single(cart.Items);
        Assert.Equal("TSH-CLS-XS", item.Sku);
        Assert.Equal("Classic Cotton T-Shirt", item.ProductName);
        Assert.Equal(2500m, item.UnitPrice);
        Assert.Equal(5000m, item.LineTotal);
        Assert.Equal(2, item.Quantity);
        Assert.NotNull(cart.Id);
    }

    [Fact]
    public async Task AddItem_SameVariantIncreasesExistingQuantity()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var context = await CreateContextAsync(connection);
        var customer = await AddCustomerAsync(context, "same-variant@test.com");
        var service = new CartService(context);
        var request = new AddCartItemRequest
        {
            ProductVariantId = SeedData.VariantBootsOneSize,
            Quantity = 2
        };

        var first = await service.AddItemAsync(customer.UserId, request);
        var second = await service.AddItemAsync(customer.UserId, request);

        Assert.Equal(CartMutationStatus.Added, first.Status);
        Assert.Equal(CartMutationStatus.Updated, second.Status);
        Assert.Equal(4, Assert.Single(second.Cart!.Items).Quantity);
        Assert.Equal(
            1,
            await context.CartItems.CountAsync(item =>
                item.ProductVariantId == SeedData.VariantBootsOneSize));
    }

    [Fact]
    public async Task UpdateItem_ReplacesQuantity()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var context = await CreateContextAsync(connection);
        var customer = await AddCustomerAsync(context, "update-cart@test.com");
        var service = new CartService(context);
        var added = await service.AddItemAsync(
            customer.UserId,
            new AddCartItemRequest
            {
                ProductVariantId = SeedData.VariantHoodieM,
                Quantity = 2
            });
        var itemId = Assert.Single(added.Cart!.Items).Id;

        var updated = await service.UpdateItemAsync(
            customer.UserId,
            itemId,
            5);

        Assert.Equal(CartMutationStatus.Updated, updated.Status);
        Assert.Equal(5, Assert.Single(updated.Cart!.Items).Quantity);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task CartMutations_RejectNonPositiveQuantity(int quantity)
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var context = await CreateContextAsync(connection);
        var customer = await AddCustomerAsync(context, "quantity-cart@test.com");
        var service = new CartService(context);

        await Assert.ThrowsAsync<ArgumentException>(() => service.AddItemAsync(
            customer.UserId,
            new AddCartItemRequest
            {
                ProductVariantId = SeedData.VariantBootsOneSize,
                Quantity = quantity
            }));
        await Assert.ThrowsAsync<ArgumentException>(() => service.UpdateItemAsync(
            customer.UserId,
            Guid.NewGuid(),
            quantity));
    }

    [Fact]
    public async Task AddItem_RejectsUnavailableVariant()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var context = await CreateContextAsync(connection);
        var customer = await AddCustomerAsync(context, "unavailable-cart@test.com");
        var variant = await context.ProductVariants.SingleAsync(item =>
            item.Id == SeedData.VariantJeansM);
        variant.IsActive = false;
        await context.SaveChangesAsync();

        var result = await new CartService(context).AddItemAsync(
            customer.UserId,
            new AddCartItemRequest
            {
                ProductVariantId = SeedData.VariantJeansM,
                Quantity = 1
            });

        Assert.Equal(CartMutationStatus.VariantUnavailable, result.Status);
        Assert.Empty(context.CartItems);
    }

    [Fact]
    public async Task AddItem_ReturnsNotFoundForUnknownVariant()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var context = await CreateContextAsync(connection);
        var customer = await AddCustomerAsync(context, "missing-variant@test.com");

        var result = await new CartService(context).AddItemAsync(
            customer.UserId,
            new AddCartItemRequest
            {
                ProductVariantId = Guid.NewGuid(),
                Quantity = 1
            });

        Assert.Equal(CartMutationStatus.VariantNotFound, result.Status);
        Assert.Empty(context.CartItems);
    }

    [Fact]
    public async Task AddItem_UsesUnreservedStockAndRejectsInsufficientQuantity()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var context = await CreateContextAsync(connection);
        var customer = await AddCustomerAsync(context, "stock-cart@test.com");
        var inventory = await context.Inventory.SingleAsync(item =>
            item.ProductVariantId == SeedData.VariantJacketL);
        inventory.QuantityOnHand = 5;
        inventory.ReservedQuantity = 2;
        await context.SaveChangesAsync();

        var result = await new CartService(context).AddItemAsync(
            customer.UserId,
            new AddCartItemRequest
            {
                ProductVariantId = SeedData.VariantJacketL,
                Quantity = 4
            });

        Assert.Equal(CartMutationStatus.InsufficientStock, result.Status);
        Assert.Empty(context.CartItems);
    }

    [Fact]
    public async Task RemoveItem_RemovesOwnedItem()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var context = await CreateContextAsync(connection);
        var customer = await AddCustomerAsync(context, "remove-cart@test.com");
        var service = new CartService(context);
        var added = await service.AddItemAsync(
            customer.UserId,
            new AddCartItemRequest
            {
                ProductVariantId = SeedData.VariantTShirtM,
                Quantity = 1
            });
        var itemId = Assert.Single(added.Cart!.Items).Id;

        var removed = await service.RemoveItemAsync(customer.UserId, itemId);

        Assert.True(removed);
        Assert.False(await context.CartItems.AnyAsync(item => item.Id == itemId));
    }

    [Fact]
    public async Task ClearCart_RemovesAllOwnedItems()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var context = await CreateContextAsync(connection);
        var customer = await AddCustomerAsync(context, "clear-cart@test.com");
        var service = new CartService(context);
        await service.AddItemAsync(
            customer.UserId,
            new AddCartItemRequest
            {
                ProductVariantId = SeedData.VariantTShirtXs,
                Quantity = 1
            });
        await service.AddItemAsync(
            customer.UserId,
            new AddCartItemRequest
            {
                ProductVariantId = SeedData.VariantBootsOneSize,
                Quantity = 1
            });

        await service.ClearCartAsync(customer.UserId);

        Assert.Empty((await service.GetCartAsync(customer.UserId)).Items);
        Assert.True(await context.Carts.AnyAsync(item =>
            item.CustomerId == customer.CustomerId));
    }

    [Fact]
    public async Task CustomerCannotUpdateOrRemoveAnotherCustomersItem()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var context = await CreateContextAsync(connection);
        var customerA = await AddCustomerAsync(context, "cart-a@test.com");
        var customerB = await AddCustomerAsync(context, "cart-b@test.com");
        var service = new CartService(context);
        var added = await service.AddItemAsync(
            customerB.UserId,
            new AddCartItemRequest
            {
                ProductVariantId = SeedData.VariantHoodieL,
                Quantity = 2
            });
        var itemId = Assert.Single(added.Cart!.Items).Id;

        var update = await service.UpdateItemAsync(
            customerA.UserId,
            itemId,
            3);
        var removed = await service.RemoveItemAsync(customerA.UserId, itemId);

        Assert.Equal(CartMutationStatus.ItemNotFound, update.Status);
        Assert.False(removed);
        Assert.Equal(
            2,
            (await context.CartItems.SingleAsync(item => item.Id == itemId)).Quantity);
    }

    [Fact]
    public async Task CartTotals_AreCalculatedFromDatabasePrices()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var context = await CreateContextAsync(connection);
        var customer = await AddCustomerAsync(context, "totals-cart@test.com");
        var service = new CartService(context);
        await service.AddItemAsync(
            customer.UserId,
            new AddCartItemRequest
            {
                ProductVariantId = SeedData.VariantTShirtXs,
                Quantity = 2
            });
        await service.AddItemAsync(
            customer.UserId,
            new AddCartItemRequest
            {
                ProductVariantId = SeedData.VariantBootsOneSize,
                Quantity = 3
            });

        var cart = await service.GetCartAsync(customer.UserId);

        Assert.Equal(5, cart.TotalQuantity);
        Assert.Equal(31700m, cart.Subtotal);
        Assert.Equal(31700m, cart.Total);
        Assert.Equal(
            cart.Subtotal,
            cart.Items.Sum(item => item.LineTotal));
    }

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

    private static async Task<(int UserId, int CustomerId)> AddCustomerAsync(
        AppDbContext context,
        string email)
    {
        var customer = new Customer
        {
            User = new User
            {
                Email = email,
                PasswordHash = "test-only-password-hash",
                FirstName = "Test",
                LastName = "Customer",
                RoleId = 1,
                IsActive = true
            }
        };

        context.Customers.Add(customer);
        await context.SaveChangesAsync();
        return (customer.UserId, customer.Id);
    }

    private static CartController CreateController(
        ICartService service,
        int? userId = null)
    {
        var claims = userId.HasValue
            ? new[]
            {
                new Claim(
                    ClaimTypes.NameIdentifier,
                    userId.Value.ToString())
            }
            : Array.Empty<Claim>();
        var identity = new ClaimsIdentity(
            claims,
            userId.HasValue ? "TestAuthentication" : null);

        return new CartController(service)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(identity)
                }
            }
        };
    }
}
