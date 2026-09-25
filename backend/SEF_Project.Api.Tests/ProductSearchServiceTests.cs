using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SEF_Project.Api.Data;
using SEF_Project.Api.Data.Configurations;
using SEF_Project.Api.DTOs.Shopping;
using SEF_Project.Api.Services.Shopping;

namespace SEF_Project.Api.Tests;

public class ProductSearchServiceTests
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

    [Fact]
    public async Task SearchAsync_SearchesProductAndVariantText()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var context = await CreateContextAsync(connection);
        var service = new ProductSearchService(context);

        var byName = await service.SearchAsync(new ProductSearchQuery
        {
            Search = "boots"
        });
        var bySku = await service.SearchAsync(new ProductSearchQuery
        {
            Search = "BTS-ANK-OS"
        });

        Assert.Single(byName.Items);
        Assert.Equal("Leather Ankle Boots", byName.Items[0].Name);
        Assert.Single(bySku.Items);
        Assert.Equal(SeedData.ProductBoots, bySku.Items[0].Id);
    }

    [Fact]
    public async Task SearchAsync_FiltersByCategory()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var context = await CreateContextAsync(connection);
        var service = new ProductSearchService(context);

        var result = await service.SearchAsync(new ProductSearchQuery
        {
            CategoryId = SeedData.CategoryBottoms
        });

        Assert.Single(result.Items);
        Assert.Equal("Slim Fit Denim Jeans", result.Items[0].Name);
    }

    [Fact]
    public async Task SearchAsync_FiltersByInclusivePriceRange()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var context = await CreateContextAsync(connection);
        var service = new ProductSearchService(context);

        var result = await service.SearchAsync(new ProductSearchQuery
        {
            MinPrice = 2000m,
            MaxPrice = 7000m
        });

        Assert.Equal(2, result.TotalCount);
        Assert.Equal(
            new[] { "Classic Cotton T-Shirt", "Fleece Pullover Hoodie" },
            result.Items.Select(item => item.Name).ToArray());
    }

    [Fact]
    public async Task SearchAsync_SortsByMinimumActiveVariantPrice()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var context = await CreateContextAsync(connection);
        var service = new ProductSearchService(context);

        var result = await service.SearchAsync(new ProductSearchQuery
        {
            SortBy = "price",
            SortDirection = "desc"
        });

        Assert.Equal(
            new[] { 12500m, 8900m, 7500m, 6500m, 2500m },
            result.Items.Select(item => item.MinimumPrice).ToArray());
    }

    [Fact]
    public async Task SearchAsync_ReturnsPaginationMetadata()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var context = await CreateContextAsync(connection);
        var service = new ProductSearchService(context);

        var result = await service.SearchAsync(new ProductSearchQuery
        {
            Page = 2,
            PageSize = 2
        });

        Assert.Equal(5, result.TotalCount);
        Assert.Equal(3, result.TotalPages);
        Assert.Equal(2, result.Page);
        Assert.Equal(2, result.PageSize);
        Assert.True(result.HasPreviousPage);
        Assert.True(result.HasNextPage);
        Assert.Equal(2, result.Items.Count);
    }

    [Fact]
    public async Task SearchAsync_ReturnsEmptyPageWhenNoProductsMatch()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var context = await CreateContextAsync(connection);
        var service = new ProductSearchService(context);

        var result = await service.SearchAsync(new ProductSearchQuery
        {
            Search = "not-a-real-product"
        });

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
        Assert.Equal(0, result.TotalPages);
        Assert.False(result.HasNextPage);
    }

    [Fact]
    public async Task SearchAsync_CombinesSearchCategoryPriceAndAvailability()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var context = await CreateContextAsync(connection);
        var service = new ProductSearchService(context);

        var result = await service.SearchAsync(new ProductSearchQuery
        {
            Search = "hoodie",
            CategoryId = SeedData.CategoryTops,
            MinPrice = 6500m,
            MaxPrice = 6900m,
            InStockOnly = true
        });

        Assert.Single(result.Items);
        Assert.Equal("Fleece Pullover Hoodie", result.Items[0].Name);
        Assert.True(result.Items[0].IsAvailable);
    }

    [Fact]
    public async Task SearchAsync_InStockOnlyExcludesProductsWithoutAvailableQuantity()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var context = await CreateContextAsync(connection);

        var inventory = await context.Inventory.SingleAsync(item =>
            item.ProductVariantId == SeedData.VariantBootsOneSize);
        inventory.ReservedQuantity = inventory.QuantityOnHand;
        await context.SaveChangesAsync();

        var result = await new ProductSearchService(context).SearchAsync(
            new ProductSearchQuery
            {
                Search = "boots",
                InStockOnly = true
            });

        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task SearchAsync_ExcludesInactiveProductsAndVariants()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var context = await CreateContextAsync(connection);

        var product = await context.Products.SingleAsync(item =>
            item.Id == SeedData.ProductJeans);
        product.IsActive = false;
        await context.SaveChangesAsync();

        var result = await new ProductSearchService(context).SearchAsync(
            new ProductSearchQuery { Search = "jeans" });

        Assert.Empty(result.Items);
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(10001, 10)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public async Task SearchAsync_RejectsInvalidPagination(int page, int pageSize)
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var context = await CreateContextAsync(connection);
        var service = new ProductSearchService(context);

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.SearchAsync(new ProductSearchQuery
            {
                Page = page,
                PageSize = pageSize
            }));

        Assert.Contains("Page", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("rating", "asc")]
    [InlineData("name", "sideways")]
    public async Task SearchAsync_RejectsUnsupportedSorting(
        string sortBy,
        string sortDirection)
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var context = await CreateContextAsync(connection);
        var service = new ProductSearchService(context);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.SearchAsync(new ProductSearchQuery
            {
                SortBy = sortBy,
                SortDirection = sortDirection
            }));
    }

    [Theory]
    [InlineData(-1, null)]
    [InlineData(null, -1)]
    [InlineData(2000, 1000)]
    public async Task SearchAsync_RejectsInvalidPriceRanges(
        int? minPrice,
        int? maxPrice)
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var context = await CreateContextAsync(connection);
        var service = new ProductSearchService(context);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.SearchAsync(new ProductSearchQuery
            {
                MinPrice = minPrice,
                MaxPrice = maxPrice
            }));
    }
}
