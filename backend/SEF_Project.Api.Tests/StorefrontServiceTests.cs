using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SEF_Project.Api.Controllers;
using SEF_Project.Api.Data;
using SEF_Project.Api.DTOs.Storefront;
using SEF_Project.Api.Models;
using SEF_Project.Api.Models.Catalog;
using SEF_Project.Api.Services.Storefront;

namespace SEF_Project.Api.Tests;

public class StorefrontServiceTests
{
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

    private static StorefrontProductQueryDto Query(
        string? search = null,
        Guid? categoryId = null,
        string? size = null,
        string? colour = null,
        decimal? minPrice = null,
        decimal? maxPrice = null,
        string? sortBy = null,
        string? sortDirection = null,
        int limit = 24) =>
        new()
        {
            Search = search,
            CategoryId = categoryId,
            Size = size,
            Colour = colour,
            MinPrice = minPrice,
            MaxPrice = maxPrice,
            SortBy = sortBy,
            SortDirection = sortDirection,
            Limit = limit
        };

    [Fact]
    public async Task GetProductsAsync_ShouldReturnPublicProjectionForSeededCatalog()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;

        var service = new StorefrontService(context);

        var products = await service.GetProductsAsync(Query());

        Assert.Equal(5, products.Count);
        Assert.Equal(
            new[]
            {
                "Classic Cotton T-Shirt",
                "Fleece Pullover Hoodie",
                "Leather Ankle Boots",
                "Quilted Field Jacket",
                "Slim Fit Denim Jeans"
            },
            products.Select(p => p.Name).ToArray());

        var tShirt = products.Single(p => p.Name == "Classic Cotton T-Shirt");

        Assert.Equal(2500m, tShirt.PriceFrom);
        Assert.Equal(2600m, tShirt.PriceTo);
        Assert.Equal("Tops", tShirt.CategoryName);
        Assert.Equal("Summer Essentials", tShirt.CollectionName);
        Assert.Equal(new[] { "XS", "M" }, tShirt.Sizes.ToArray());
        Assert.True(tShirt.InStock);
        Assert.EndsWith(".svg", tShirt.ImageUrl);
        Assert.Equal(0, tShirt.ReviewCount);
        Assert.Equal(0m, tShirt.AverageRating);
    }

    [Fact]
    public async Task GetProductsAsync_ShouldIncludePublishedReviewAggregates()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;

        var tShirtId = await context.Products
            .Where(p => p.Name == "Classic Cotton T-Shirt")
            .Select(p => p.Id)
            .SingleAsync();

        var customerA = await AddCustomerAsync(context, "a@test.com", "Asha", "Perera");
        var customerB = await AddCustomerAsync(context, "b@test.com", "Bimal", "Silva");
        var customerC = await AddCustomerAsync(context, "c@test.com", "Chloe", "Fernando");

        context.Reviews.AddRange(
            new Review { ProductId = tShirtId, CustomerId = customerA, Rating = 5 },
            new Review { ProductId = tShirtId, CustomerId = customerB, Rating = 4 },
            new Review
            {
                ProductId = tShirtId,
                CustomerId = customerC,
                Rating = 1,
                IsPublished = false
            });
        await context.SaveChangesAsync();

        var service = new StorefrontService(context);

        var product = await service.GetProductByIdAsync(tShirtId);

        Assert.NotNull(product);
        Assert.Equal(2, product!.ReviewCount);
        Assert.Equal(4.5m, product.AverageRating);

        var list = await service.GetProductsAsync(Query());
        var tShirt = list.Single(p => p.Name == "Classic Cotton T-Shirt");

        Assert.Equal(2, tShirt.ReviewCount);
        Assert.Equal(4.5m, tShirt.AverageRating);
    }

    [Fact]
    public async Task GetProductsAsync_ShouldExposeBuyableVariantsWithColourSwatches()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;

        var service = new StorefrontService(context);

        var products = await service.GetProductsAsync(Query());
        var tShirt = products.Single(p => p.Name == "Classic Cotton T-Shirt");

        Assert.Equal(2, tShirt.Variants.Count);

        var first = tShirt.Variants[0];
        Assert.NotEqual(Guid.Empty, first.Id);
        Assert.Equal("XS", first.SizeName);
        Assert.Equal("Black", first.ColourName);
        Assert.Equal("#000000", first.ColourHex);
        Assert.Equal(2500m, first.Price);
        Assert.True(first.InStock);

        Assert.Equal(
            new[] { "Black", "White" },
            tShirt.Colours.Select(colour => colour.Name).ToArray());
        Assert.Equal(
            new[] { "#000000", "#ffffff" },
            tShirt.Colours.Select(colour => colour.HexCode).ToArray());
    }

    [Fact]
    public async Task GetProductsAsync_ShouldExcludeInactiveProductsAndVariants()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;

        // Deactivate one variant: it should no longer influence the price range.
        var largeTShirt = await context.ProductVariants
            .SingleAsync(v => v.Sku == "TSH-CLS-M");
        largeTShirt.IsActive = false;

        // Deactivate a whole product: it should disappear from the storefront.
        var boots = await context.Products
            .SingleAsync(p => p.Name == "Leather Ankle Boots");
        boots.IsActive = false;

        await context.SaveChangesAsync();

        var service = new StorefrontService(context);

        var products = await service.GetProductsAsync(Query());

        Assert.Equal(4, products.Count);
        Assert.DoesNotContain(products, p => p.Name == "Leather Ankle Boots");

        var tShirt = products.Single(p => p.Name == "Classic Cotton T-Shirt");

        Assert.Equal(2500m, tShirt.PriceFrom);
        Assert.Equal(2500m, tShirt.PriceTo);
        Assert.Single(tShirt.Variants);
        Assert.Equal(new[] { "XS" }, tShirt.Sizes.ToArray());
        Assert.Equal(
            new[] { "Black" },
            tShirt.Colours.Select(colour => colour.Name).ToArray());
    }

    [Fact]
    public async Task GetProductsAsync_ShouldFilterBySearchAndCategory()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;

        var service = new StorefrontService(context);

        var bySearch = await service.GetProductsAsync(Query(search: "hoodie"));

        var match = Assert.Single(bySearch);
        Assert.Equal("Fleece Pullover Hoodie", match.Name);

        var topsId = await context.Categories
            .Where(c => c.Name == "Tops")
            .Select(c => c.Id)
            .SingleAsync();

        var byCategory = await service.GetProductsAsync(Query(categoryId: topsId));

        Assert.Equal(2, byCategory.Count);
        Assert.All(byCategory, p => Assert.Equal("Tops", p.CategoryName));
    }

    [Fact]
    public async Task GetProductsAsync_ShouldFilterBySizeColourAndPrice()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;

        var service = new StorefrontService(context);

        var oneSize = await service.GetProductsAsync(Query(size: "One Size"));
        Assert.Equal("Leather Ankle Boots", Assert.Single(oneSize).Name);

        var navy = await service.GetProductsAsync(Query(colour: "Navy"));
        Assert.Equal(2, navy.Count);
        Assert.All(
            navy,
            p => Assert.Contains("Navy", p.Colours.Select(colour => colour.Name)));

        var priceBand = await service.GetProductsAsync(
            Query(minPrice: 8000m, maxPrice: 9000m));
        Assert.Equal("Leather Ankle Boots", Assert.Single(priceBand).Name);
    }

    [Fact]
    public async Task GetProductsAsync_ShouldSortByPrice()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;

        var service = new StorefrontService(context);

        var cheapest = await service.GetProductsAsync(
            Query(sortBy: "price", sortDirection: "asc", limit: 3));

        Assert.Equal(
            new[]
            {
                "Classic Cotton T-Shirt",
                "Fleece Pullover Hoodie",
                "Slim Fit Denim Jeans"
            },
            cheapest.Select(p => p.Name).ToArray());

        var mostExpensive = await service.GetProductsAsync(
            Query(sortBy: "price", sortDirection: "desc", limit: 2));

        Assert.Equal(
            new[] { "Quilted Field Jacket", "Leather Ankle Boots" },
            mostExpensive.Select(p => p.Name).ToArray());
    }

    [Fact]
    public async Task GetProductsAsync_ShouldRespectLimit()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;

        var service = new StorefrontService(context);

        var products = await service.GetProductsAsync(Query(limit: 2));

        Assert.Equal(2, products.Count);
    }

    [Fact]
    public async Task GetProductByIdAsync_ShouldReturnPublicProduct_OrNull()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;

        var service = new StorefrontService(context);

        var tShirtId = await context.Products
            .Where(p => p.Name == "Classic Cotton T-Shirt")
            .Select(p => p.Id)
            .SingleAsync();

        var product = await service.GetProductByIdAsync(tShirtId);

        Assert.NotNull(product);
        Assert.Equal("Classic Cotton T-Shirt", product!.Name);
        Assert.Equal(2, product.Variants.Count);

        Assert.Null(await service.GetProductByIdAsync(Guid.NewGuid()));

        var boots = await context.Products
            .SingleAsync(p => p.Name == "Leather Ankle Boots");
        boots.IsActive = false;
        await context.SaveChangesAsync();

        Assert.Null(await service.GetProductByIdAsync(boots.Id));
    }

    private static async Task<int> AddCustomerAsync(
        AppDbContext context,
        string email,
        string firstName,
        string lastName)
    {
        var customer = new Customer
        {
            User = new User
            {
                Email = email,
                PasswordHash = "test-only-password-hash",
                FirstName = firstName,
                LastName = lastName,
                RoleId = 1,
                IsActive = true
            }
        };

        context.Customers.Add(customer);
        await context.SaveChangesAsync();
        return customer.Id;
    }

    [Fact]
    public void StorefrontController_ShouldBeAnonymous_AndNotExposeStaffFields()
    {
        var controller = typeof(StorefrontController);

        Assert.NotNull(controller.GetCustomAttribute<AllowAnonymousAttribute>());
        Assert.Null(controller.GetCustomAttribute<AuthorizeAttribute>());

        var exposedProperties = typeof(StorefrontProductResponseDto)
            .GetProperties()
            .Select(p => p.Name)
            .ToArray();

        Assert.DoesNotContain("SupplierName", exposedProperties);
        Assert.DoesNotContain("QuantityOnHand", exposedProperties);
        Assert.DoesNotContain("ReservedQuantity", exposedProperties);
        Assert.DoesNotContain("IsActive", exposedProperties);
    }
}
