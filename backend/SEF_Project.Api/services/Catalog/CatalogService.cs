using Microsoft.EntityFrameworkCore;
using SEF_Project.Api.Data;
using SEF_Project.Api.DTOs.Catalog;
using SEF_Project.Api.Models.Catalog;
using SEF_Project.Api.Models.Enums;

namespace SEF_Project.Api.Services.Catalog;

public class CatalogService : ICatalogService
{
    private readonly AppDbContext _context;

    public CatalogService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<CategoryResponseDto>> GetCategoriesAsync(
        CancellationToken cancellationToken = default) =>
        await _context.Categories
            .AsNoTracking()
            .OrderBy(c => c.Name)
            .Select(c => MapCategory(c))
            .ToListAsync(cancellationToken);

    public async Task<CategoryResponseDto?> GetCategoryByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var category = await _context.Categories
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        return category is null ? null : MapCategory(category);
    }

    public async Task<CategoryResponseDto> CreateCategoryAsync(
        CategoryCreateDto request,
        CancellationToken cancellationToken = default)
    {
        await EnsureUniqueCategoryNameAsync(
            request.Name,
            excludingId: null,
            cancellationToken);

        var category = new Category
        {
            Name = request.Name.Trim(),
            Description = NullIfWhitespace(request.Description),
            IsActive = request.IsActive
        };

        _context.Categories.Add(category);
        await _context.SaveChangesAsync(cancellationToken);

        return MapCategory(category);
    }

    public async Task<CategoryResponseDto?> UpdateCategoryAsync(
        Guid id,
        CategoryUpdateDto request,
        CancellationToken cancellationToken = default)
    {
        var category = await _context.Categories
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        if (category is null)
        {
            return null;
        }

        await EnsureUniqueCategoryNameAsync(
            request.Name,
            excludingId: id,
            cancellationToken);

        category.Name = request.Name.Trim();
        category.Description = NullIfWhitespace(request.Description);
        category.IsActive = request.IsActive;

        await _context.SaveChangesAsync(cancellationToken);

        return MapCategory(category);
    }

    public async Task<bool> DeleteCategoryAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var category = await _context.Categories
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        if (category is null)
        {
            return false;
        }

        category.IsActive = false;
        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<List<CollectionResponseDto>> GetCollectionsAsync(
        CancellationToken cancellationToken = default) =>
        await _context.Collections
            .AsNoTracking()
            .OrderBy(c => c.Name)
            .Select(c => MapCollection(c))
            .ToListAsync(cancellationToken);

    public async Task<CollectionResponseDto?> GetCollectionByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var collection = await _context.Collections
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        return collection is null ? null : MapCollection(collection);
    }

    public async Task<CollectionResponseDto> CreateCollectionAsync(
        CollectionCreateDto request,
        CancellationToken cancellationToken = default)
    {
        await EnsureUniqueCollectionNameAsync(
            request.Name,
            excludingId: null,
            cancellationToken);

        var collection = new Collection
        {
            Name = request.Name.Trim(),
            Description = NullIfWhitespace(request.Description),
            IsActive = request.IsActive
        };

        _context.Collections.Add(collection);
        await _context.SaveChangesAsync(cancellationToken);

        return MapCollection(collection);
    }

    public async Task<CollectionResponseDto?> UpdateCollectionAsync(
        Guid id,
        CollectionUpdateDto request,
        CancellationToken cancellationToken = default)
    {
        var collection = await _context.Collections
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        if (collection is null)
        {
            return null;
        }

        await EnsureUniqueCollectionNameAsync(
            request.Name,
            excludingId: id,
            cancellationToken);

        collection.Name = request.Name.Trim();
        collection.Description = NullIfWhitespace(request.Description);
        collection.IsActive = request.IsActive;

        await _context.SaveChangesAsync(cancellationToken);

        return MapCollection(collection);
    }

    public async Task<bool> DeleteCollectionAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var collection = await _context.Collections
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        if (collection is null)
        {
            return false;
        }

        collection.IsActive = false;
        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<List<SizeResponseDto>> GetSizesAsync(
        CancellationToken cancellationToken = default) =>
        await _context.Sizes
            .AsNoTracking()
            .OrderBy(s => s.DisplayOrder)
            .ThenBy(s => s.Name)
            .Select(s => MapSize(s))
            .ToListAsync(cancellationToken);

    public async Task<SizeResponseDto?> GetSizeByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var size = await _context.Sizes
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

        return size is null ? null : MapSize(size);
    }

    public async Task<SizeResponseDto> CreateSizeAsync(
        SizeCreateDto request,
        CancellationToken cancellationToken = default)
    {
        await EnsureUniqueSizeNameAsync(
            request.Name,
            excludingId: null,
            cancellationToken);

        var size = new Size
        {
            Name = request.Name.Trim(),
            Description = NullIfWhitespace(request.Description),
            DisplayOrder = request.DisplayOrder,
            IsActive = request.IsActive
        };

        _context.Sizes.Add(size);
        await _context.SaveChangesAsync(cancellationToken);

        return MapSize(size);
    }

    public async Task<SizeResponseDto?> UpdateSizeAsync(
        Guid id,
        SizeUpdateDto request,
        CancellationToken cancellationToken = default)
    {
        var size = await _context.Sizes
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

        if (size is null)
        {
            return null;
        }

        await EnsureUniqueSizeNameAsync(
            request.Name,
            excludingId: id,
            cancellationToken);

        size.Name = request.Name.Trim();
        size.Description = NullIfWhitespace(request.Description);
        size.DisplayOrder = request.DisplayOrder;
        size.IsActive = request.IsActive;

        await _context.SaveChangesAsync(cancellationToken);

        return MapSize(size);
    }

    public async Task<bool> DeleteSizeAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var size = await _context.Sizes
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

        if (size is null)
        {
            return false;
        }

        size.IsActive = false;
        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<List<ColourResponseDto>> GetColoursAsync(
        CancellationToken cancellationToken = default) =>
        await _context.Colours
            .AsNoTracking()
            .OrderBy(c => c.Name)
            .Select(c => MapColour(c))
            .ToListAsync(cancellationToken);

    public async Task<ColourResponseDto?> GetColourByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var colour = await _context.Colours
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        return colour is null ? null : MapColour(colour);
    }

    public async Task<ColourResponseDto> CreateColourAsync(
        ColourCreateDto request,
        CancellationToken cancellationToken = default)
    {
        await EnsureUniqueColourNameAsync(
            request.Name,
            excludingId: null,
            cancellationToken);

        var colour = new Colour
        {
            Name = request.Name.Trim(),
            HexCode = NullIfWhitespace(request.HexCode),
            IsActive = request.IsActive
        };

        _context.Colours.Add(colour);
        await _context.SaveChangesAsync(cancellationToken);

        return MapColour(colour);
    }

    public async Task<ColourResponseDto?> UpdateColourAsync(
        Guid id,
        ColourUpdateDto request,
        CancellationToken cancellationToken = default)
    {
        var colour = await _context.Colours
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        if (colour is null)
        {
            return null;
        }

        await EnsureUniqueColourNameAsync(
            request.Name,
            excludingId: id,
            cancellationToken);

        colour.Name = request.Name.Trim();
        colour.HexCode = NullIfWhitespace(request.HexCode);
        colour.IsActive = request.IsActive;

        await _context.SaveChangesAsync(cancellationToken);

        return MapColour(colour);
    }

    public async Task<bool> DeleteColourAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var colour = await _context.Colours
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        if (colour is null)
        {
            return false;
        }

        colour.IsActive = false;
        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<List<ProductResponseDto>> GetProductsAsync(
        CancellationToken cancellationToken = default)
    {
        var products = await ProductQuery()
            .AsNoTracking()
            .OrderBy(p => p.Name)
            .ToListAsync(cancellationToken);

        return products.Select(MapProduct).ToList();
    }

    public async Task<ProductResponseDto?> GetProductByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var product = await ProductQuery()
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        return product is null ? null : MapProduct(product);
    }

    public async Task<ProductResponseDto> CreateProductAsync(
        ProductCreateDto request,
        CancellationToken cancellationToken = default)
    {
        await ValidateProductReferencesAsync(
            request.CategoryId,
            request.CollectionId,
            request.SupplierId,
            cancellationToken);

        var product = new Product
        {
            Name = request.Name.Trim(),
            Description = NullIfWhitespace(request.Description),
            CategoryId = request.CategoryId,
            CollectionId = request.CollectionId,
            SupplierId = request.SupplierId,
            IsActive = request.IsActive
        };

        _context.Products.Add(product);
        await _context.SaveChangesAsync(cancellationToken);

        var created = await ProductQuery()
            .AsNoTracking()
            .SingleAsync(p => p.Id == product.Id, cancellationToken);

        return MapProduct(created);
    }

    public async Task<ProductResponseDto?> UpdateProductAsync(
        Guid id,
        ProductUpdateDto request,
        CancellationToken cancellationToken = default)
    {
        var product = await _context.Products
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (product is null)
        {
            return null;
        }

        await ValidateProductReferencesAsync(
            request.CategoryId,
            request.CollectionId,
            request.SupplierId,
            cancellationToken);

        product.Name = request.Name.Trim();
        product.Description = NullIfWhitespace(request.Description);
        product.CategoryId = request.CategoryId;
        product.CollectionId = request.CollectionId;
        product.SupplierId = request.SupplierId;
        product.IsActive = request.IsActive;

        await _context.SaveChangesAsync(cancellationToken);

        var updated = await ProductQuery()
            .AsNoTracking()
            .SingleAsync(p => p.Id == id, cancellationToken);

        return MapProduct(updated);
    }

    public async Task<bool> DeleteProductAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var product = await _context.Products
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (product is null)
        {
            return false;
        }

        product.IsActive = false;
        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<List<ProductVariantResponseDto>> GetVariantsAsync(
        Guid? productId = null,
        CancellationToken cancellationToken = default)
    {
        var query = VariantQuery().AsNoTracking();

        if (productId is not null)
        {
            query = query.Where(v => v.ProductId == productId.Value);
        }

        var variants = await query
            .OrderBy(v => v.Product.Name)
            .ThenBy(v => v.Sku)
            .ToListAsync(cancellationToken);

        return variants.Select(MapVariant).ToList();
    }

    public async Task<ProductVariantResponseDto?> GetVariantByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var variant = await VariantQuery()
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == id, cancellationToken);

        return variant is null ? null : MapVariant(variant);
    }

    public async Task<ProductVariantResponseDto> CreateVariantAsync(
        ProductVariantCreateDto request,
        CancellationToken cancellationToken = default)
    {
        await ValidateVariantReferencesAsync(
            request.ProductId,
            request.SizeId,
            request.ColourId,
            cancellationToken);
        await EnsureUniqueSkuAsync(
            request.Sku,
            excludingId: null,
            cancellationToken);
        await EnsureUniqueVariantCombinationAsync(
            request.ProductId,
            request.SizeId,
            request.ColourId,
            excludingId: null,
            cancellationToken);

        await using var transaction =
            await _context.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var variant = new ProductVariant
            {
                ProductId = request.ProductId,
                SizeId = request.SizeId,
                ColourId = request.ColourId,
                Sku = request.Sku.Trim(),
                Name = request.Name.Trim(),
                Price = Math.Round(request.Price, 2),
                IsActive = request.IsActive
            };

            _context.ProductVariants.Add(variant);
            _context.Inventory.Add(new InventoryStock
            {
                ProductVariant = variant,
                QuantityOnHand = request.InitialQuantityOnHand,
                ReservedQuantity = 0,
                ReorderLevel = request.ReorderLevel
            });

            if (request.InitialQuantityOnHand > 0)
            {
                _context.InventoryTransactions.Add(new StockTransaction
                {
                    ProductVariant = variant,
                    Type = InventoryTransactionType.Receipt,
                    QuantityChange = request.InitialQuantityOnHand,
                    QuantityOnHandAfter = request.InitialQuantityOnHand,
                    Reference = "Initial stock"
                });
            }

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            var created = await VariantQuery()
                .AsNoTracking()
                .SingleAsync(v => v.Id == variant.Id, cancellationToken);

            return MapVariant(created);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<ProductVariantResponseDto?> UpdateVariantAsync(
        Guid id,
        ProductVariantUpdateDto request,
        CancellationToken cancellationToken = default)
    {
        var variant = await _context.ProductVariants
            .Include(v => v.InventoryStock)
            .FirstOrDefaultAsync(v => v.Id == id, cancellationToken);

        if (variant is null)
        {
            return null;
        }

        await ValidateSizeAndColourAsync(
            request.SizeId,
            request.ColourId,
            cancellationToken);
        await EnsureUniqueSkuAsync(
            request.Sku,
            excludingId: id,
            cancellationToken);
        await EnsureUniqueVariantCombinationAsync(
            variant.ProductId,
            request.SizeId,
            request.ColourId,
            excludingId: id,
            cancellationToken);

        variant.SizeId = request.SizeId;
        variant.ColourId = request.ColourId;
        variant.Sku = request.Sku.Trim();
        variant.Name = request.Name.Trim();
        variant.Price = Math.Round(request.Price, 2);
        variant.IsActive = request.IsActive;

        if (variant.InventoryStock is null)
        {
            variant.InventoryStock = new InventoryStock
            {
                ProductVariantId = variant.Id,
                ReorderLevel = request.ReorderLevel
            };
        }
        else
        {
            variant.InventoryStock.ReorderLevel = request.ReorderLevel;
        }

        await _context.SaveChangesAsync(cancellationToken);

        var updated = await VariantQuery()
            .AsNoTracking()
            .SingleAsync(v => v.Id == id, cancellationToken);

        return MapVariant(updated);
    }

    public async Task<bool> DeleteVariantAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var variant = await _context.ProductVariants
            .FirstOrDefaultAsync(v => v.Id == id, cancellationToken);

        if (variant is null)
        {
            return false;
        }

        variant.IsActive = false;
        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }

    private IQueryable<Product> ProductQuery() =>
        _context.Products
            .Include(p => p.Category)
            .Include(p => p.Collection)
            .Include(p => p.Supplier)
            .Include(p => p.Variants)
                .ThenInclude(v => v.Size)
            .Include(p => p.Variants)
                .ThenInclude(v => v.Colour)
            .Include(p => p.Variants)
                .ThenInclude(v => v.InventoryStock);

    private IQueryable<ProductVariant> VariantQuery() =>
        _context.ProductVariants
            .Include(v => v.Product)
            .Include(v => v.Size)
            .Include(v => v.Colour)
            .Include(v => v.InventoryStock);

    private async Task ValidateProductReferencesAsync(
        Guid categoryId,
        Guid collectionId,
        Guid? supplierId,
        CancellationToken cancellationToken)
    {
        if (!await _context.Categories.AnyAsync(c => c.Id == categoryId, cancellationToken))
        {
            throw new ArgumentException("Category was not found.");
        }

        if (!await _context.Collections.AnyAsync(c => c.Id == collectionId, cancellationToken))
        {
            throw new ArgumentException("Collection was not found.");
        }

        if (supplierId is not null
            && !await _context.Suppliers.AnyAsync(s => s.Id == supplierId.Value, cancellationToken))
        {
            throw new ArgumentException("Supplier was not found.");
        }
    }

    private async Task ValidateVariantReferencesAsync(
        Guid productId,
        Guid sizeId,
        Guid colourId,
        CancellationToken cancellationToken)
    {
        if (!await _context.Products.AnyAsync(p => p.Id == productId, cancellationToken))
        {
            throw new ArgumentException("Product was not found.");
        }

        await ValidateSizeAndColourAsync(sizeId, colourId, cancellationToken);
    }

    private async Task ValidateSizeAndColourAsync(
        Guid sizeId,
        Guid colourId,
        CancellationToken cancellationToken)
    {
        if (!await _context.Sizes.AnyAsync(s => s.Id == sizeId, cancellationToken))
        {
            throw new ArgumentException("Size was not found.");
        }

        if (!await _context.Colours.AnyAsync(c => c.Id == colourId, cancellationToken))
        {
            throw new ArgumentException("Colour was not found.");
        }
    }

    private async Task EnsureUniqueCategoryNameAsync(
        string name,
        Guid? excludingId,
        CancellationToken cancellationToken)
    {
        var normalized = name.Trim().ToLowerInvariant();

        var exists = await _context.Categories.AnyAsync(
            c => c.Name.ToLower() == normalized
                 && (excludingId == null || c.Id != excludingId.Value),
            cancellationToken);

        if (exists)
        {
            throw new InvalidOperationException("A category with this name already exists.");
        }
    }

    private async Task EnsureUniqueCollectionNameAsync(
        string name,
        Guid? excludingId,
        CancellationToken cancellationToken)
    {
        var normalized = name.Trim().ToLowerInvariant();

        var exists = await _context.Collections.AnyAsync(
            c => c.Name.ToLower() == normalized
                 && (excludingId == null || c.Id != excludingId.Value),
            cancellationToken);

        if (exists)
        {
            throw new InvalidOperationException("A collection with this name already exists.");
        }
    }

    private async Task EnsureUniqueSizeNameAsync(
        string name,
        Guid? excludingId,
        CancellationToken cancellationToken)
    {
        var normalized = name.Trim().ToLowerInvariant();

        var exists = await _context.Sizes.AnyAsync(
            s => s.Name.ToLower() == normalized
                 && (excludingId == null || s.Id != excludingId.Value),
            cancellationToken);

        if (exists)
        {
            throw new InvalidOperationException("A size with this name already exists.");
        }
    }

    private async Task EnsureUniqueColourNameAsync(
        string name,
        Guid? excludingId,
        CancellationToken cancellationToken)
    {
        var normalized = name.Trim().ToLowerInvariant();

        var exists = await _context.Colours.AnyAsync(
            c => c.Name.ToLower() == normalized
                 && (excludingId == null || c.Id != excludingId.Value),
            cancellationToken);

        if (exists)
        {
            throw new InvalidOperationException("A colour with this name already exists.");
        }
    }

    private async Task EnsureUniqueSkuAsync(
        string sku,
        Guid? excludingId,
        CancellationToken cancellationToken)
    {
        var normalized = sku.Trim().ToLowerInvariant();

        var exists = await _context.ProductVariants.AnyAsync(
            v => v.Sku.ToLower() == normalized
                 && (excludingId == null || v.Id != excludingId.Value),
            cancellationToken);

        if (exists)
        {
            throw new InvalidOperationException("A product variant with this SKU already exists.");
        }
    }

    private async Task EnsureUniqueVariantCombinationAsync(
        Guid productId,
        Guid sizeId,
        Guid colourId,
        Guid? excludingId,
        CancellationToken cancellationToken)
    {
        var exists = await _context.ProductVariants.AnyAsync(
            v => v.ProductId == productId
                 && v.SizeId == sizeId
                 && v.ColourId == colourId
                 && (excludingId == null || v.Id != excludingId.Value),
            cancellationToken);

        if (exists)
        {
            throw new InvalidOperationException(
                "This product, size and colour variant already exists.");
        }
    }

    private static ProductResponseDto MapProduct(Product product) =>
        new()
        {
            Id = product.Id,
            Name = product.Name,
            Description = product.Description,
            CategoryId = product.CategoryId,
            CategoryName = product.Category?.Name ?? string.Empty,
            CollectionId = product.CollectionId,
            CollectionName = product.Collection?.Name ?? string.Empty,
            SupplierId = product.SupplierId,
            SupplierName = product.Supplier?.Name,
            IsActive = product.IsActive,
            CreatedAt = product.CreatedAt,
            UpdatedAt = product.UpdatedAt,
            Variants = product.Variants
                .OrderBy(v => v.Sku)
                .Select(MapVariant)
                .ToList()
        };

    private static ProductVariantResponseDto MapVariant(ProductVariant variant) =>
        new()
        {
            Id = variant.Id,
            ProductId = variant.ProductId,
            ProductName = variant.Product?.Name ?? string.Empty,
            SizeId = variant.SizeId,
            SizeName = variant.Size?.Name ?? string.Empty,
            ColourId = variant.ColourId,
            ColourName = variant.Colour?.Name ?? string.Empty,
            Sku = variant.Sku,
            Name = variant.Name,
            Price = variant.Price,
            IsActive = variant.IsActive,
            Inventory = variant.InventoryStock is null
                ? null
                : InventoryService.MapInventory(variant.InventoryStock),
            CreatedAt = variant.CreatedAt,
            UpdatedAt = variant.UpdatedAt
        };

    private static CategoryResponseDto MapCategory(Category category) =>
        new()
        {
            Id = category.Id,
            Name = category.Name,
            Description = category.Description,
            IsActive = category.IsActive,
            CreatedAt = category.CreatedAt,
            UpdatedAt = category.UpdatedAt
        };

    private static CollectionResponseDto MapCollection(Collection collection) =>
        new()
        {
            Id = collection.Id,
            Name = collection.Name,
            Description = collection.Description,
            IsActive = collection.IsActive,
            CreatedAt = collection.CreatedAt,
            UpdatedAt = collection.UpdatedAt
        };

    private static SizeResponseDto MapSize(Size size) =>
        new()
        {
            Id = size.Id,
            Name = size.Name,
            Description = size.Description,
            DisplayOrder = size.DisplayOrder,
            IsActive = size.IsActive,
            CreatedAt = size.CreatedAt,
            UpdatedAt = size.UpdatedAt
        };

    private static ColourResponseDto MapColour(Colour colour) =>
        new()
        {
            Id = colour.Id,
            Name = colour.Name,
            HexCode = colour.HexCode,
            IsActive = colour.IsActive,
            CreatedAt = colour.CreatedAt,
            UpdatedAt = colour.UpdatedAt
        };

    private static string? NullIfWhitespace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
