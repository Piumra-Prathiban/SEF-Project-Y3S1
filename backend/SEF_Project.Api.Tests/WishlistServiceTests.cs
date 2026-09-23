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

public class WishlistServiceTests
{
    [Fact]
    public void WishlistController_RequiresAuthentication()
    {
        var attribute = typeof(WishlistController)
            .GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(attribute);
    }

    [Fact]
    public async Task GetWishlist_ReturnsUnauthorizedWithoutUserClaim()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var context = await CreateContextAsync(connection);
        var controller = CreateController(new WishlistService(context));

        var response = await controller.GetWishlist(CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(response.Result);
    }

    [Fact]
    public async Task GetWishlist_ReturnsOnlyAuthenticatedCustomersItems()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var context = await CreateContextAsync(connection);
        var customerA = await AddCustomerAsync(context, "customer-a@test.com");
        var customerB = await AddCustomerAsync(context, "customer-b@test.com");
        await AddWishlistItemAsync(
            context,
            customerA.CustomerId,
            SeedData.ProductMargherita);
        await AddWishlistItemAsync(
            context,
            customerB.CustomerId,
            SeedData.ProductCola);

        var controller = CreateController(
            new WishlistService(context),
            customerA.UserId);

        var action = await controller.GetWishlist(CancellationToken.None);
        var response = Assert.IsType<WishlistResponse>(
            Assert.IsType<OkObjectResult>(action.Result).Value);

        Assert.Equal(1, response.Count);
        Assert.Equal(
            SeedData.ProductMargherita,
            Assert.Single(response.Items).ProductId);
        Assert.DoesNotContain(
            response.Items,
            item => item.ProductId == SeedData.ProductCola);
    }

    [Fact]
    public async Task AddItem_AddsProductAndReturnsCreated()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var context = await CreateContextAsync(connection);
        var customer = await AddCustomerAsync(context, "add@test.com");
        var controller = CreateController(
            new WishlistService(context),
            customer.UserId);

        var action = await controller.AddItem(
            new AddWishlistItemRequest
            {
                ProductId = SeedData.ProductMargherita
            },
            CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(action.Result);
        var item = Assert.IsType<WishlistItemResponse>(created.Value);
        Assert.Equal(SeedData.ProductMargherita, item.ProductId);
        Assert.Equal(nameof(WishlistController.GetWishlist), created.ActionName);
        Assert.True(await context.WishlistItems.AnyAsync(wishlistItem =>
            wishlistItem.ProductId == SeedData.ProductMargherita &&
            wishlistItem.Wishlist.CustomerId == customer.CustomerId));
    }

    [Fact]
    public async Task AddItem_ReturnsConflictForDuplicateProduct()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var context = await CreateContextAsync(connection);
        var customer = await AddCustomerAsync(context, "duplicate@test.com");
        var controller = CreateController(
            new WishlistService(context),
            customer.UserId);
        var request = new AddWishlistItemRequest
        {
            ProductId = SeedData.ProductPepperoni
        };

        _ = await controller.AddItem(request, CancellationToken.None);
        var duplicate = await controller.AddItem(request, CancellationToken.None);

        var result = Assert.IsType<ObjectResult>(duplicate.Result);
        Assert.Equal(StatusCodes.Status409Conflict, result.StatusCode);
        Assert.Equal(
            1,
            await context.WishlistItems.CountAsync(item =>
                item.ProductId == SeedData.ProductPepperoni));
    }

    [Fact]
    public async Task AddItem_ReturnsNotFoundForInvalidProduct()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var context = await CreateContextAsync(connection);
        var customer = await AddCustomerAsync(context, "invalid@test.com");
        var controller = CreateController(
            new WishlistService(context),
            customer.UserId);

        var action = await controller.AddItem(
            new AddWishlistItemRequest { ProductId = Guid.NewGuid() },
            CancellationToken.None);

        var result = Assert.IsType<ObjectResult>(action.Result);
        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
        Assert.Empty(context.WishlistItems);
    }

    [Fact]
    public async Task RemoveItem_RemovesOwnedProduct()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var context = await CreateContextAsync(connection);
        var customer = await AddCustomerAsync(context, "remove@test.com");
        await AddWishlistItemAsync(
            context,
            customer.CustomerId,
            SeedData.ProductCarbonara);
        var controller = CreateController(
            new WishlistService(context),
            customer.UserId);

        var result = await controller.RemoveItem(
            SeedData.ProductCarbonara,
            CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        Assert.False(await context.WishlistItems.AnyAsync(item =>
            item.ProductId == SeedData.ProductCarbonara));
    }

    [Fact]
    public async Task RemoveItem_CannotRemoveAnotherCustomersProduct()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var context = await CreateContextAsync(connection);
        var customerA = await AddCustomerAsync(context, "owner@test.com");
        var customerB = await AddCustomerAsync(context, "other@test.com");
        await AddWishlistItemAsync(
            context,
            customerB.CustomerId,
            SeedData.ProductTiramisu);
        var controller = CreateController(
            new WishlistService(context),
            customerA.UserId);

        var result = await controller.RemoveItem(
            SeedData.ProductTiramisu,
            CancellationToken.None);

        var notFound = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status404NotFound, notFound.StatusCode);
        Assert.True(await context.WishlistItems.AnyAsync(item =>
            item.ProductId == SeedData.ProductTiramisu &&
            item.Wishlist.CustomerId == customerB.CustomerId));
    }

    [Fact]
    public async Task GetWishlist_ReturnsEmptyResponseWhenWishlistDoesNotExist()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var context = await CreateContextAsync(connection);
        var customer = await AddCustomerAsync(context, "empty@test.com");
        var service = new WishlistService(context);

        var response = await service.GetWishlistAsync(customer.UserId);

        Assert.Null(response.Id);
        Assert.Empty(response.Items);
        Assert.Equal(0, response.Count);
        Assert.False(await context.Wishlists.AnyAsync(item =>
            item.CustomerId == customer.CustomerId));
    }

    [Fact]
    public async Task GetCount_ReturnsCurrentCustomersItemCount()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var context = await CreateContextAsync(connection);
        var customer = await AddCustomerAsync(context, "count@test.com");
        await AddWishlistItemAsync(
            context,
            customer.CustomerId,
            SeedData.ProductMargherita,
            SeedData.ProductCola);
        var controller = CreateController(
            new WishlistService(context),
            customer.UserId);

        var action = await controller.GetCount(CancellationToken.None);
        var response = Assert.IsType<WishlistCountResponse>(
            Assert.IsType<OkObjectResult>(action.Result).Value);

        Assert.Equal(2, response.Count);
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

    private static async Task AddWishlistItemAsync(
        AppDbContext context,
        int customerId,
        params Guid[] productIds)
    {
        var wishlist = new Wishlist { CustomerId = customerId };

        foreach (var productId in productIds)
        {
            wishlist.Items.Add(new WishlistItem { ProductId = productId });
        }

        context.Wishlists.Add(wishlist);
        await context.SaveChangesAsync();
    }

    private static WishlistController CreateController(
        IWishlistService service,
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
        var controller = new WishlistController(service)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(identity)
                }
            }
        };

        return controller;
    }
}
