using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SEF_Project.Api.Data;
using SEF_Project.Api.Data.Configurations;
using SEF_Project.Api.DTOs.Shopping;
using SEF_Project.Api.Models;
using SEF_Project.Api.Models.Shopping;
using SEF_Project.Api.Services.Profile;
using SEF_Project.Api.Services.Recommendations;
using SEF_Project.Api.Services.Shopping;

namespace SEF_Project.Api.Tests;

public class PersonalStylistToolTests
{
    [Fact]
    public async Task CustomerAndWishlistTools_LoadAuthenticatedServerContext()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var context = await CreateContextAsync(connection);
        var customer = new Customer
        {
            User = new User
            {
                Email = "stylist@test.com",
                PasswordHash = "test-only-password-hash",
                FirstName = "Asha",
                LastName = "Perera",
                RoleId = 1,
                IsActive = true
            }
        };
        context.Customers.Add(customer);
        await context.SaveChangesAsync();
        context.Wishlists.Add(new Wishlist
        {
            CustomerId = customer.Id,
            Items =
            {
                new WishlistItem { ProductId = SeedData.ProductMargherita }
            }
        });
        await context.SaveChangesAsync();
        var preferences = new RecommendationContext(
            "Dinner",
            5000m,
            Array.Empty<string>(),
            null,
            null);

        var customerOutput = await new CustomerPreferenceTool(
            new ProfileService(context)).ExecuteAsync(
                new CustomerPreferenceToolInput(customer.UserId, preferences));
        var wishlistOutput = await new WishlistTool(
            new WishlistService(context)).ExecuteAsync(
                new WishlistToolInput(customer.UserId));

        Assert.Equal(customer.Id, customerOutput.Customer.CustomerId);
        Assert.Equal("Asha", customerOutput.Customer.FirstName);
        Assert.Equal(
            SeedData.ProductMargherita,
            Assert.Single(wishlistOutput.Items).ProductId);
    }

    [Fact]
    public async Task ProductSearchTool_UsesAuthoritativePriceAndInventoryData()
    {
        var productId = Guid.NewGuid();
        var availableVariant = Guid.NewGuid();
        var productSearch = new RecordingProductSearchService
        {
            Response = new PagedProductResponse
            {
                Items = new[]
                {
                    new ShoppingProductResponse
                    {
                        Id = productId,
                        Name = "Work Shirt",
                        IsAvailable = true,
                        Variants = new[]
                        {
                            new ShoppingProductVariantResponse
                            {
                                Id = Guid.NewGuid(),
                                Sku = "NO-STOCK",
                                Name = "No stock",
                                Price = 50m,
                                AvailableQuantity = 0,
                                IsAvailable = false
                            },
                            new ShoppingProductVariantResponse
                            {
                                Id = availableVariant,
                                Sku = "AVAILABLE",
                                Name = "Available",
                                Price = 100m,
                                AvailableQuantity = 2,
                                IsAvailable = true
                            },
                            new ShoppingProductVariantResponse
                            {
                                Id = Guid.NewGuid(),
                                Sku = "OVER-BUDGET",
                                Name = "Over budget",
                                Price = 500m,
                                AvailableQuantity = 5,
                                IsAvailable = true
                            }
                        }
                    }
                },
                Page = 1,
                PageSize = 12,
                TotalCount = 1,
                TotalPages = 1
            }
        };
        var tool = new ProductSearchTool(productSearch);

        var output = await tool.ExecuteAsync(
            new ProductSearchToolInput("Work", 200m));

        Assert.Equal("Work", productSearch.Query!.Search);
        Assert.Equal(200m, productSearch.Query.MaxPrice);
        Assert.True(productSearch.Query.InStockOnly);
        Assert.Equal("price", productSearch.Query.SortBy);
        var product = Assert.Single(output.Products);
        Assert.Equal(100m, product.MinimumAvailablePrice);
        Assert.Equal(
            availableVariant,
            Assert.Single(product.AvailableVariants).Id);
    }

    [Fact]
    public async Task ProductAvailabilityTool_RejectsMismatchedProductVariantPair()
    {
        var actualProductId = Guid.NewGuid();
        var requestedProductId = Guid.NewGuid();
        var variantId = Guid.NewGuid();
        var service = new RecordingAvailabilityService
        {
            Results = new[]
            {
                new AvailableProductVariant(
                    actualProductId,
                    variantId,
                    "Real Product",
                    "Medium",
                    "SKU-M",
                    120m,
                    2)
            }
        };
        var tool = new ProductAvailabilityTool(service);

        var output = await tool.ExecuteAsync(
            new ProductAvailabilityToolInput(new[]
            {
                new ProductAvailabilityToolItem(requestedProductId, variantId)
            }));

        Assert.Empty(output.AvailableVariants);
        Assert.Equal(new[] { variantId }, service.VariantIds);
    }

    [Fact]
    public async Task ProductAvailabilityService_ReturnsOnlyRealAvailableVariants()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var context = await CreateContextAsync(connection);
        var availableVariantId = await context.ProductVariants
            .Where(variant =>
                variant.IsActive &&
                variant.Product.IsActive &&
                variant.Inventory != null &&
                variant.Inventory.QuantityOnHand -
                    variant.Inventory.ReservedQuantity > 0)
            .Select(variant => variant.Id)
            .FirstAsync();
        var service = new ProductAvailabilityService(context);

        var results = await service.GetAvailableVariantsAsync(
            new[] { availableVariantId, Guid.NewGuid() });

        var result = Assert.Single(results);
        Assert.Equal(availableVariantId, result.VariantId);
        Assert.True(result.AvailableQuantity > 0);
        Assert.True(result.Price >= 0);
    }

    [Fact]
    public async Task EveryToolRejectsMalformedInputBeforeCallingDomainServices()
    {
        var invalidContext = new RecommendationContext(
            "",
            -1m,
            Array.Empty<string>(),
            null,
            null);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            new CustomerPreferenceTool(null!).ExecuteAsync(
                new CustomerPreferenceToolInput(0, invalidContext)));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            new WishlistTool(null!).ExecuteAsync(new WishlistToolInput(0)));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            new ProductSearchTool(null!).ExecuteAsync(
                new ProductSearchToolInput(new string('X', 101), null)));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            new ProductAvailabilityTool(null!).ExecuteAsync(
                new ProductAvailabilityToolInput(new[]
                {
                    new ProductAvailabilityToolItem(Guid.Empty, Guid.Empty)
                })));
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

    private sealed class RecordingProductSearchService : IProductSearchService
    {
        public ProductSearchQuery? Query { get; private set; }

        public PagedProductResponse Response { get; init; } = null!;

        public Task<PagedProductResponse> SearchAsync(
            ProductSearchQuery query,
            CancellationToken cancellationToken = default)
        {
            Query = query;
            return Task.FromResult(Response);
        }
    }

    private sealed class RecordingAvailabilityService : IProductAvailabilityService
    {
        public IReadOnlyCollection<Guid>? VariantIds { get; private set; }

        public IReadOnlyList<AvailableProductVariant> Results { get; init; } =
            Array.Empty<AvailableProductVariant>();

        public Task<IReadOnlyList<AvailableProductVariant>> GetAvailableVariantsAsync(
            IReadOnlyCollection<Guid> variantIds,
            CancellationToken cancellationToken = default)
        {
            VariantIds = variantIds;
            return Task.FromResult(Results);
        }
    }
}
