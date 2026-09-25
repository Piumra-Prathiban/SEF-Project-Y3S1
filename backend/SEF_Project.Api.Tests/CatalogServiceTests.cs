using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SEF_Project.Api.Data;
using SEF_Project.Api.DTOs.Catalog;
using SEF_Project.Api.Models.Catalog;
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
    public async Task CategoryCrud_ShouldCreateReadUpdateAndSoftDelete()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;

        var service = new CatalogService(context);

        var created = await service.CreateCategoryAsync(new CategoryCreateDto
        {
            Name = "  Salads  ",
            Description = "  Fresh sides  "
        });

        var read = await service.GetCategoryByIdAsync(created.Id);
        var updated = await service.UpdateCategoryAsync(created.Id, new CategoryUpdateDto
        {
            Name = "Healthy Salads",
            Description = "Updated",
            IsActive = true
        });
        var deleted = await service.DeleteCategoryAsync(created.Id);
        var stored = await context.Categories.SingleAsync(c => c.Id == created.Id);

        Assert.Equal("Salads", created.Name);
        Assert.Equal("Fresh sides", created.Description);
        Assert.NotNull(read);
        Assert.Equal("Healthy Salads", updated?.Name);
        Assert.True(deleted);
        Assert.False(stored.IsActive);
    }

    [Fact]
    public async Task CreateCategoryAsync_ShouldRejectDuplicateName()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;

        var service = new CatalogService(context);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateCategoryAsync(new CategoryCreateDto { Name = "tops" }));

        Assert.Equal("A category with this name already exists.", exception.Message);
    }

    [Fact]
    public async Task CollectionCrud_ShouldCreateReadUpdateAndSoftDelete()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;

        var service = new CatalogService(context);

        var created = await service.CreateCollectionAsync(new CollectionCreateDto
        {
            Name = "Seasonal Specials",
            Description = "Limited items"
        });
        var read = await service.GetCollectionByIdAsync(created.Id);
        var updated = await service.UpdateCollectionAsync(created.Id, new CollectionUpdateDto
        {
            Name = "Weekend Specials",
            IsActive = true
        });
        var deleted = await service.DeleteCollectionAsync(created.Id);
        var stored = await context.Collections.SingleAsync(c => c.Id == created.Id);

        Assert.NotNull(read);
        Assert.Equal("Weekend Specials", updated?.Name);
        Assert.True(deleted);
        Assert.False(stored.IsActive);
    }

    [Fact]
    public async Task CreateCollectionAsync_ShouldRejectDuplicateName()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;

        var service = new CatalogService(context);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateCollectionAsync(new CollectionCreateDto { Name = "summer essentials" }));

        Assert.Equal("A collection with this name already exists.", exception.Message);
    }

    [Fact]
    public async Task SizeCrud_ShouldCreateReadUpdateAndSoftDelete()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;

        var service = new CatalogService(context);

        var created = await service.CreateSizeAsync(new SizeCreateDto
        {
            Name = "Family",
            DisplayOrder = 99
        });
        var read = await service.GetSizeByIdAsync(created.Id);
        var updated = await service.UpdateSizeAsync(created.Id, new SizeUpdateDto
        {
            Name = "Party",
            DisplayOrder = 100,
            IsActive = true
        });
        var deleted = await service.DeleteSizeAsync(created.Id);
        var stored = await context.Sizes.SingleAsync(s => s.Id == created.Id);

        Assert.NotNull(read);
        Assert.Equal("Party", updated?.Name);
        Assert.Equal(100, updated?.DisplayOrder);
        Assert.True(deleted);
        Assert.False(stored.IsActive);
    }

    [Fact]
    public async Task ColourCrud_ShouldCreateReadUpdateAndSoftDelete()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;

        var service = new CatalogService(context);

        var created = await service.CreateColourAsync(new ColourCreateDto
        {
            Name = "Red",
            HexCode = "#FF0000"
        });
        var read = await service.GetColourByIdAsync(created.Id);
        var updated = await service.UpdateColourAsync(created.Id, new ColourUpdateDto
        {
            Name = "Crimson",
            HexCode = "#DC143C",
            IsActive = true
        });
        var deleted = await service.DeleteColourAsync(created.Id);
        var stored = await context.Colours.SingleAsync(c => c.Id == created.Id);

        Assert.NotNull(read);
        Assert.Equal("Crimson", updated?.Name);
        Assert.Equal("#DC143C", updated?.HexCode);
        Assert.True(deleted);
        Assert.False(stored.IsActive);
    }

    [Fact]
    public async Task ProductCrud_ShouldCreateReadUpdateAndSoftDelete()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;

        var categoryId = await context.Categories.Select(c => c.Id).FirstAsync();
        var collectionId = await context.Collections.Select(c => c.Id).FirstAsync();
        var updatedCategoryId = await context.Categories
            .Where(c => c.Id != categoryId)
            .Select(c => c.Id)
            .FirstAsync();
        var service = new CatalogService(context);

        var created = await service.CreateProductAsync(new ProductCreateDto
        {
            Name = "  Garlic Bread  ",
            Description = "  Starter  ",
            CategoryId = categoryId,
            CollectionId = collectionId
        });
        var read = await service.GetProductByIdAsync(created.Id);
        var updated = await service.UpdateProductAsync(created.Id, new ProductUpdateDto
        {
            Name = "Cheesy Garlic Bread",
            CategoryId = updatedCategoryId,
            CollectionId = collectionId,
            IsActive = true
        });
        var deleted = await service.DeleteProductAsync(created.Id);
        var stored = await context.Products.SingleAsync(p => p.Id == created.Id);

        Assert.Equal("Garlic Bread", created.Name);
        Assert.Equal("Starter", created.Description);
        Assert.NotNull(read);
        Assert.Equal("Cheesy Garlic Bread", updated?.Name);
        Assert.Equal(updatedCategoryId, updated?.CategoryId);
        Assert.True(deleted);
        Assert.False(stored.IsActive);
    }

    [Fact]
    public async Task CreateProductAsync_ShouldRejectInvalidCategory()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;

        var collectionId = await context.Collections.Select(c => c.Id).FirstAsync();
        var service = new CatalogService(context);

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateProductAsync(new ProductCreateDto
            {
                Name = "Invalid Product",
                CategoryId = Guid.NewGuid(),
                CollectionId = collectionId
            }));

        Assert.Equal("Category was not found.", exception.Message);
    }

    [Fact]
    public async Task CreateProductAsync_ShouldRejectInvalidCollection()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;

        var categoryId = await context.Categories.Select(c => c.Id).FirstAsync();
        var service = new CatalogService(context);

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateProductAsync(new ProductCreateDto
            {
                Name = "Invalid Product",
                CategoryId = categoryId,
                CollectionId = Guid.NewGuid()
            }));

        Assert.Equal("Collection was not found.", exception.Message);
    }

    [Fact]
    public async Task GetProductsAsync_ShouldSearchByProductName()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;

        var service = new CatalogService(context);

        var response = await service.GetProductsAsync(new ProductQueryDto
        {
            Search = "hoodie"
        });

        var product = Assert.Single(response.Items);
        Assert.Equal("Fleece Pullover Hoodie", product.Name);
        Assert.Equal(1, response.TotalItems);
        Assert.Equal(1, response.TotalPages);
    }

    [Fact]
    public async Task GetProductsAsync_ShouldFilterByCategoryCollectionStatusAndPrice()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;

        var pepperoni = await context.Products
            .SingleAsync(p => p.Name == "Fleece Pullover Hoodie");
        var service = new CatalogService(context);

        var response = await service.GetProductsAsync(new ProductQueryDto
        {
            CategoryId = pepperoni.CategoryId,
            CollectionId = pepperoni.CollectionId,
            IsActive = true,
            MinPrice = 6400m,
            MaxPrice = 6600m
        });

        var product = Assert.Single(response.Items);
        Assert.Equal("Fleece Pullover Hoodie", product.Name);
    }

    [Fact]
    public async Task GetProductsAsync_ShouldSortByPrice()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;

        var service = new CatalogService(context);

        var response = await service.GetProductsAsync(new ProductQueryDto
        {
            SortBy = "price",
            SortDirection = "asc",
            PageSize = 3
        });

        Assert.Equal(new[]
        {
            "Classic Cotton T-Shirt",
            "Fleece Pullover Hoodie",
            "Slim Fit Denim Jeans"
        }, response.Items.Select(p => p.Name).ToArray());
    }

    [Fact]
    public async Task GetProductsAsync_ShouldReturnRequestedPageMetadata()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;

        var service = new CatalogService(context);

        var response = await service.GetProductsAsync(new ProductQueryDto
        {
            SortBy = "name",
            SortDirection = "asc",
            Page = 2,
            PageSize = 2
        });

        Assert.Equal(2, response.Page);
        Assert.Equal(2, response.PageSize);
        Assert.Equal(5, response.TotalItems);
        Assert.Equal(3, response.TotalPages);
        Assert.Equal(new[]
        {
            "Leather Ankle Boots",
            "Quilted Field Jacket"
        }, response.Items.Select(p => p.Name).ToArray());
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

    [Fact]
    public async Task UpdateVariantAsync_ShouldUpdateVariantAndReorderLevel()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;

        var variant = await context.ProductVariants
            .AsNoTracking()
            .SingleAsync(v => v.Sku == "TSH-CLS-XS");
        var mediumSizeId = await context.Sizes
            .Where(s => s.Name == "M")
            .Select(s => s.Id)
            .SingleAsync();
        var service = new CatalogService(context);

        var updated = await service.UpdateVariantAsync(variant.Id, new ProductVariantUpdateDto
        {
            SizeId = mediumSizeId,
            ColourId = variant.ColourId,
            Sku = "TSH-CLS-M-BLK",
            Name = "M / Black",
            Price = 1500.456m,
            ReorderLevel = 8,
            IsActive = true
        });

        var inventory = await context.Inventory
            .SingleAsync(i => i.ProductVariantId == variant.Id);

        Assert.NotNull(updated);
        Assert.Equal("TSH-CLS-M-BLK", updated.Sku);
        Assert.Equal(1500.46m, updated.Price);
        Assert.Equal(8, inventory.ReorderLevel);
    }

    [Fact]
    public async Task UpdateVariantAsync_ShouldRejectDuplicateSku()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;

        var target = await context.ProductVariants
            .AsNoTracking()
            .SingleAsync(v => v.Sku == "TSH-CLS-XS");
        var duplicate = await context.ProductVariants
            .AsNoTracking()
            .SingleAsync(v => v.Sku == "HOD-FLC-M");
        var service = new CatalogService(context);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UpdateVariantAsync(target.Id, new ProductVariantUpdateDto
            {
                SizeId = target.SizeId,
                ColourId = target.ColourId,
                Sku = duplicate.Sku,
                Name = target.Name,
                Price = target.Price,
                ReorderLevel = 1,
                IsActive = true
            }));

        Assert.Equal("A product variant with this SKU already exists.", exception.Message);
    }

    [Fact]
    public async Task UpdateVariantAsync_ShouldRejectDuplicateProductSizeColour()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;

        var target = await context.ProductVariants
            .AsNoTracking()
            .SingleAsync(v => v.Sku == "TSH-CLS-XS");
        var existingCombination = await context.ProductVariants
            .AsNoTracking()
            .SingleAsync(v => v.Sku == "TSH-CLS-M");
        var service = new CatalogService(context);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UpdateVariantAsync(target.Id, new ProductVariantUpdateDto
            {
                SizeId = existingCombination.SizeId,
                ColourId = existingCombination.ColourId,
                Sku = "TSH-CLS-XS-DUP",
                Name = "Duplicate Combination",
                Price = target.Price,
                ReorderLevel = 1,
                IsActive = true
            }));

        Assert.Equal("This product, size and colour variant already exists.", exception.Message);
    }

    [Fact]
    public async Task UpdateVariantAsync_ShouldRejectInvalidSize()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;

        var target = await context.ProductVariants.AsNoTracking().FirstAsync();
        var service = new CatalogService(context);

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.UpdateVariantAsync(target.Id, new ProductVariantUpdateDto
            {
                SizeId = Guid.NewGuid(),
                ColourId = target.ColourId,
                Sku = "VALID-SKU-001",
                Name = "Invalid Size",
                Price = target.Price,
                ReorderLevel = 1,
                IsActive = true
            }));

        Assert.Equal("Size was not found.", exception.Message);
    }

    [Fact]
    public async Task UpdateVariantAsync_ShouldRejectInvalidColour()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;

        var target = await context.ProductVariants.AsNoTracking().FirstAsync();
        var service = new CatalogService(context);

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.UpdateVariantAsync(target.Id, new ProductVariantUpdateDto
            {
                SizeId = target.SizeId,
                ColourId = Guid.NewGuid(),
                Sku = "VALID-SKU-002",
                Name = "Invalid Colour",
                Price = target.Price,
                ReorderLevel = 1,
                IsActive = true
            }));

        Assert.Equal("Colour was not found.", exception.Message);
    }

    [Fact]
    public async Task CreateVariantAsync_ShouldRejectNegativePriceAtDatabaseConstraint()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;

        var productId = await context.Products.Select(p => p.Id).FirstAsync();
        var existingSizeIds = await context.ProductVariants
            .Where(v => v.ProductId == productId)
            .Select(v => v.SizeId)
            .ToListAsync();
        var sizeId = await context.Sizes
            .Where(s => !existingSizeIds.Contains(s.Id))
            .Select(s => s.Id)
            .FirstAsync();
        var colourId = await context.Colours.Select(c => c.Id).FirstAsync();
        var service = new CatalogService(context);

        await Assert.ThrowsAsync<DbUpdateException>(() =>
            service.CreateVariantAsync(new ProductVariantCreateDto
            {
                ProductId = productId,
                SizeId = sizeId,
                ColourId = colourId,
                Sku = "NEGATIVE-PRICE",
                Name = "Negative Price",
                Price = -1m
            }));
    }
}
