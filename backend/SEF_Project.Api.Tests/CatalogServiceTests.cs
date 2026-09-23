using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SEF_Project.Api.Data;
using SEF_Project.Api.DTOs.Catalog;
using SEF_Project.Api.Services.Catalog;

namespace SEF_Project.Api.Tests;

public class CatalogServiceTests
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

    [Fact]
    public async Task CreateVariantAsync_ShouldThrow_WhenProductMissing()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;

        var sizeId = await context.Sizes
            .Select(s => s.Id)
            .FirstAsync();
        var colourId = await context.Colours
            .Select(c => c.Id)
            .FirstAsync();
        var service = new CatalogService(context);

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateVariantAsync(new ProductVariantCreateDto
            {
                ProductId = Guid.NewGuid(),
                SizeId = sizeId,
                ColourId = colourId,
                Sku = "NEW-MISSING-PRODUCT",
                Name = "Missing Product Variant",
                Price = 100m
            }));

        Assert.Equal("Product was not found.", exception.Message);
    }

    [Fact]
    public async Task CreateVariantAsync_ShouldThrow_WhenSizeMissing()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;

        var productId = await context.Products
            .Select(p => p.Id)
            .FirstAsync();
        var colourId = await context.Colours
            .Select(c => c.Id)
            .FirstAsync();
        var service = new CatalogService(context);

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateVariantAsync(new ProductVariantCreateDto
            {
                ProductId = productId,
                SizeId = Guid.NewGuid(),
                ColourId = colourId,
                Sku = "NEW-MISSING-SIZE",
                Name = "Missing Size Variant",
                Price = 100m
            }));

        Assert.Equal("Size was not found.", exception.Message);
    }

    [Fact]
    public async Task CreateVariantAsync_ShouldThrow_WhenColourMissing()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;

        var productId = await context.Products
            .Select(p => p.Id)
            .FirstAsync();
        var sizeId = await context.Sizes
            .Select(s => s.Id)
            .FirstAsync();
        var service = new CatalogService(context);

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateVariantAsync(new ProductVariantCreateDto
            {
                ProductId = productId,
                SizeId = sizeId,
                ColourId = Guid.NewGuid(),
                Sku = "NEW-MISSING-COLOUR",
                Name = "Missing Colour Variant",
                Price = 100m
            }));

        Assert.Equal("Colour was not found.", exception.Message);
    }

    [Fact]
    public async Task CreateVariantAsync_ShouldThrow_WhenSkuAlreadyExists()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;

        var existing = await context.ProductVariants
            .AsNoTracking()
            .FirstAsync();
        var productId = await context.Products
            .Where(p => p.Id != existing.ProductId)
            .Select(p => p.Id)
            .FirstAsync();
        var sizeId = await context.Sizes
            .Where(s => s.Id != existing.SizeId)
            .Select(s => s.Id)
            .FirstAsync();
        var colourId = await context.Colours
            .Select(c => c.Id)
            .FirstAsync();
        var service = new CatalogService(context);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateVariantAsync(new ProductVariantCreateDto
            {
                ProductId = productId,
                SizeId = sizeId,
                ColourId = colourId,
                Sku = existing.Sku,
                Name = "Duplicate SKU Variant",
                Price = 100m
            }));

        Assert.Equal(
            "A product variant with this SKU already exists.",
            exception.Message);
    }

    [Fact]
    public async Task CreateVariantAsync_ShouldThrow_WhenVariantCombinationExists()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;

        var existing = await context.ProductVariants
            .AsNoTracking()
            .FirstAsync();
        var service = new CatalogService(context);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateVariantAsync(new ProductVariantCreateDto
            {
                ProductId = existing.ProductId,
                SizeId = existing.SizeId,
                ColourId = existing.ColourId,
                Sku = "UNIQUE-COMBO-SKU",
                Name = "Duplicate Combination Variant",
                Price = 100m
            }));

        Assert.Equal(
            "This product, size and colour variant already exists.",
            exception.Message);
    }

    [Fact]
    public async Task CreateVariantAsync_ShouldCreateInventoryAndInitialStockTransaction()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;

        var productId = await context.Products
            .Select(p => p.Id)
            .FirstAsync();
        var existingSizeIds = await context.ProductVariants
            .Where(v => v.ProductId == productId)
            .Select(v => v.SizeId)
            .ToListAsync();
        var sizeId = await context.Sizes
            .Where(s => !existingSizeIds.Contains(s.Id))
            .Select(s => s.Id)
            .FirstAsync();
        var colourId = await context.Colours
            .Select(c => c.Id)
            .FirstAsync();
        var service = new CatalogService(context);

        var response = await service.CreateVariantAsync(new ProductVariantCreateDto
        {
            ProductId = productId,
            SizeId = sizeId,
            ColourId = colourId,
            Sku = "NEW-VALID-SKU",
            Name = "New Valid Variant",
            Price = 500m,
            InitialQuantityOnHand = 12,
            ReorderLevel = 3
        });

        var inventory = await context.Inventory
            .SingleAsync(i => i.ProductVariantId == response.Id);
        var transaction = await context.InventoryTransactions
            .SingleAsync(t => t.ProductVariantId == response.Id);

        Assert.Equal(12, inventory.QuantityOnHand);
        Assert.Equal(3, inventory.ReorderLevel);
        Assert.Equal(12, transaction.QuantityChange);
        Assert.Equal(12, transaction.QuantityOnHandAfter);
    }

    [Fact]
    public async Task DeleteVariantAsync_ShouldMarkVariantInactive()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;

        var variantId = await context.ProductVariants
            .Select(v => v.Id)
            .FirstAsync();
        var service = new CatalogService(context);

        var deleted = await service.DeleteVariantAsync(variantId);

        var variant = await context.ProductVariants
            .SingleAsync(v => v.Id == variantId);
        Assert.True(deleted);
        Assert.False(variant.IsActive);
    }
}
