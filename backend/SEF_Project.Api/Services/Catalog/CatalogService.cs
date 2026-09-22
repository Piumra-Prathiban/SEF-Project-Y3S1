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

    #region Categories

    public async Task<List<CategoryResponse>> GetCategoriesAsync(
        bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Categories.AsNoTracking();

        if (!includeInactive)
        {
            query = query.Where(c => c.IsActive);
        }

        return await query
            .OrderBy(c => c.Name)
            .Select(c => new CategoryResponse
            {
                Id = c.Id,
                Name = c.Name,
                Description = c.Description,
                IsActive = c.IsActive,
                ProductCount = c.ProductCategories.Count(pc => pc.Product.IsActive),
                CreatedAt = c.CreatedAt,
                UpdatedAt = c.UpdatedAt
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<CategoryResponse?> GetCategoryByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var category = await _context.Categories
            .AsNoTracking()
            .Where(c => c.Id == id)
            .Select(c => new CategoryResponse
            {
                Id = c.Id,
                Name = c.Name,
                Description = c.Description,
                IsActive = c.IsActive,
                ProductCount = c.ProductCategories.Count(pc => pc.Product.IsActive),
                CreatedAt = c.CreatedAt,
                UpdatedAt = c.UpdatedAt
            })
            .FirstOrDefaultAsync(cancellationToken);

        return category;
    }

    public async Task<CategoryResponse> CreateCategoryAsync(
        CreateCategoryRequest request,
        CancellationToken cancellationToken = default)
    {
        var trimmedName = request.Name.Trim();

        var exists = await _context.Categories
            .AnyAsync(c => c.Name.ToLower() == trimmedName.ToLower(), cancellationToken);

        if (exists)
        {
            throw new InvalidOperationException($"Category with name '{trimmedName}' already exists.");
        }

        var category = new Category
        {
            Name = trimmedName,
            Description = request.Description?.Trim(),
            IsActive = request.IsActive
        };

        _context.Categories.Add(category);
        await _context.SaveChangesAsync(cancellationToken);

        return new CategoryResponse
        {
            Id = category.Id,
            Name = category.Name,
            Description = category.Description,
            IsActive = category.IsActive,
            ProductCount = 0,
            CreatedAt = category.CreatedAt,
            UpdatedAt = category.UpdatedAt
        };
    }

    public async Task<CategoryResponse?> UpdateCategoryAsync(
        Guid id,
        UpdateCategoryRequest request,
        CancellationToken cancellationToken = default)
    {
        var category = await _context.Categories.FindAsync(new object[] { id }, cancellationToken);

        if (category is null)
        {
            return null;
        }

        var trimmedName = request.Name.Trim();

        var duplicate = await _context.Categories
            .AnyAsync(c => c.Id != id && c.Name.ToLower() == trimmedName.ToLower(), cancellationToken);

        if (duplicate)
        {
            throw new InvalidOperationException($"Another category with name '{trimmedName}' already exists.");
        }

        category.Name = trimmedName;
        category.Description = request.Description?.Trim();
        category.IsActive = request.IsActive;

        await _context.SaveChangesAsync(cancellationToken);

        var productCount = await _context.ProductCategories
            .CountAsync(pc => pc.CategoryId == id && pc.Product.IsActive, cancellationToken);

        return new CategoryResponse
        {
            Id = category.Id,
            Name = category.Name,
            Description = category.Description,
            IsActive = category.IsActive,
            ProductCount = productCount,
            CreatedAt = category.CreatedAt,
            UpdatedAt = category.UpdatedAt
        };
    }

    public async Task<bool> DeleteCategoryAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var category = await _context.Categories
            .Include(c => c.ProductCategories)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        if (category is null)
        {
            return false;
        }

        if (category.ProductCategories.Any())
        {
            category.IsActive = false;
        }
        else
        {
            _context.Categories.Remove(category);
        }

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    #endregion

    #region Products

    public async Task<ProductListResponse> GetProductsAsync(
        ProductQuery query,
        CancellationToken cancellationToken = default)
    {
        var productsQuery = _context.Products
            .AsNoTracking()
            .Include(p => p.Supplier)
            .Include(p => p.ProductCategories)
                .ThenInclude(pc => pc.Category)
            .Include(p => p.Variants)
                .ThenInclude(v => v.Inventory)
            .Include(p => p.Images)
            .AsQueryable();

        if (query.IsActive.HasValue)
        {
            productsQuery = productsQuery.Where(p => p.IsActive == query.IsActive.Value);
        }

        if (query.CategoryId.HasValue)
        {
            productsQuery = productsQuery.Where(p =>
                p.ProductCategories.Any(pc => pc.CategoryId == query.CategoryId.Value));
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToLower();
            productsQuery = productsQuery.Where(p =>
                p.Name.ToLower().Contains(search) ||
                (p.Description != null && p.Description.ToLower().Contains(search)) ||
                p.Variants.Any(v => v.Sku.ToLower().Contains(search) || v.Name.ToLower().Contains(search)));
        }

        var totalCount = await productsQuery.CountAsync(cancellationToken);

        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var pageProducts = await productsQuery
            .OrderBy(p => p.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = pageProducts.Select(p => new ProductSummaryResponse
        {
            Id = p.Id,
            Name = p.Name,
            Description = p.Description,
            SupplierId = p.SupplierId,
            SupplierName = p.Supplier != null ? p.Supplier.Name : null,
            IsActive = p.IsActive,
            PrimaryImageUrl = p.Images
                .OrderByDescending(i => i.IsPrimary)
                .ThenBy(i => i.DisplayOrder)
                .Select(i => i.ImageUrl)
                .FirstOrDefault(),
            MinPrice = p.Variants.Where(v => v.IsActive).Select(v => v.Price).DefaultIfEmpty(0m).Min(),
            MaxPrice = p.Variants.Where(v => v.IsActive).Select(v => v.Price).DefaultIfEmpty(0m).Max(),
            VariantCount = p.Variants.Count(v => v.IsActive),
            TotalQuantityOnHand = p.Variants.Where(v => v.IsActive && v.Inventory != null).Sum(v => v.Inventory!.QuantityOnHand),
            CategoryNames = p.ProductCategories.Select(pc => pc.Category.Name).ToList(),
            CategoryIds = p.ProductCategories.Select(pc => pc.CategoryId).ToList(),
            CreatedAt = p.CreatedAt,
            UpdatedAt = p.UpdatedAt
        }).ToList();

        return new ProductListResponse
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<ProductDetailResponse?> GetProductByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var product = await _context.Products
            .AsNoTracking()
            .Include(p => p.Supplier)
            .Include(p => p.ProductCategories)
                .ThenInclude(pc => pc.Category)
            .Include(p => p.Variants)
                .ThenInclude(v => v.Inventory)
            .Include(p => p.Images)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (product is null)
        {
            return null;
        }

        return MapToDetailResponse(product);
    }

    public async Task<ProductDetailResponse> CreateProductAsync(
        CreateProductRequest request,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        var product = new Product
        {
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            SupplierId = request.SupplierId,
            IsActive = request.IsActive
        };

        _context.Products.Add(product);

        if (request.CategoryIds.Any())
        {
            var validCategoryIds = await _context.Categories
                .Where(c => request.CategoryIds.Contains(c.Id))
                .Select(c => c.Id)
                .ToListAsync(cancellationToken);

            foreach (var catId in validCategoryIds)
            {
                product.ProductCategories.Add(new ProductCategory
                {
                    ProductId = product.Id,
                    CategoryId = catId
                });
            }
        }

        if (request.InitialVariants != null && request.InitialVariants.Any())
        {
            var skus = request.InitialVariants.Select(v => v.Sku.Trim()).ToList();
            var duplicateSkuInDb = await _context.ProductVariants
                .AnyAsync(v => skus.Contains(v.Sku), cancellationToken);

            if (duplicateSkuInDb)
            {
                throw new InvalidOperationException("One or more variant SKUs already exist in the catalog.");
            }

            foreach (var vReq in request.InitialVariants)
            {
                var variant = new ProductVariant
                {
                    ProductId = product.Id,
                    Sku = vReq.Sku.Trim(),
                    Name = vReq.Name.Trim(),
                    Size = vReq.Size?.Trim(),
                    Colour = vReq.Colour?.Trim(),
                    Price = vReq.Price,
                    IsActive = vReq.IsActive
                };

                var inventory = new Inventory
                {
                    ProductVariantId = variant.Id,
                    ProductVariant = variant,
                    QuantityOnHand = Math.Max(0, vReq.InitialStock),
                    ReservedQuantity = 0,
                    ReorderLevel = Math.Max(0, vReq.ReorderLevel)
                };

                variant.Inventory = inventory;
                _context.ProductVariants.Add(variant);
                _context.Inventory.Add(inventory);

                if (vReq.InitialStock > 0)
                {
                    _context.InventoryTransactions.Add(new InventoryTransaction
                    {
                        ProductVariantId = variant.Id,
                        Type = InventoryTransactionType.Receipt,
                        QuantityChange = vReq.InitialStock,
                        QuantityOnHandAfter = vReq.InitialStock,
                        Reference = "INITIAL-STOCK",
                        Note = "Initial inventory registered on product creation."
                    });
                }
            }
        }

        if (request.InitialImages != null && request.InitialImages.Any())
        {
            var isAnyPrimary = request.InitialImages.Any(i => i.IsPrimary);
            var index = 0;

            foreach (var imgReq in request.InitialImages)
            {
                product.Images.Add(new ProductImage
                {
                    ProductId = product.Id,
                    ImageUrl = imgReq.ImageUrl.Trim(),
                    AltText = imgReq.AltText?.Trim(),
                    IsPrimary = isAnyPrimary ? imgReq.IsPrimary : index == 0,
                    DisplayOrder = imgReq.DisplayOrder
                });
                index++;
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return (await GetProductByIdAsync(product.Id, cancellationToken))!;
    }

    public async Task<ProductDetailResponse?> UpdateProductAsync(
        Guid id,
        UpdateProductRequest request,
        CancellationToken cancellationToken = default)
    {
        var product = await _context.Products
            .Include(p => p.ProductCategories)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (product is null)
        {
            return null;
        }

        product.Name = request.Name.Trim();
        product.Description = request.Description?.Trim();
        product.SupplierId = request.SupplierId;
        product.IsActive = request.IsActive;

        // Synchronize categories
        product.ProductCategories.Clear();
        if (request.CategoryIds.Any())
        {
            var validCategoryIds = await _context.Categories
                .Where(c => request.CategoryIds.Contains(c.Id))
                .Select(c => c.Id)
                .ToListAsync(cancellationToken);

            foreach (var catId in validCategoryIds)
            {
                product.ProductCategories.Add(new ProductCategory
                {
                    ProductId = product.Id,
                    CategoryId = catId
                });
            }
        }

        await _context.SaveChangesAsync(cancellationToken);

        return await GetProductByIdAsync(id, cancellationToken);
    }

    public async Task<bool> DeleteProductAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var product = await _context.Products
            .Include(p => p.Variants)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (product is null)
        {
            return false;
        }

        var variantIds = product.Variants.Select(v => v.Id).ToList();

        var hasOrders = await _context.OrderItems
            .AnyAsync(oi => variantIds.Contains(oi.ProductVariantId), cancellationToken);

        if (hasOrders)
        {
            product.IsActive = false;
            foreach (var v in product.Variants)
            {
                v.IsActive = false;
            }
        }
        else
        {
            _context.Products.Remove(product);
        }

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    #endregion

    #region Variants

    public async Task<ProductVariantResponse> CreateVariantAsync(
        Guid productId,
        CreateProductVariantRequest request,
        CancellationToken cancellationToken = default)
    {
        var product = await _context.Products.FindAsync(new object[] { productId }, cancellationToken);

        if (product is null)
        {
            throw new InvalidOperationException($"Product with ID {productId} does not exist.");
        }

        var sku = request.Sku.Trim();
        var skuExists = await _context.ProductVariants
            .AnyAsync(v => v.Sku.ToLower() == sku.ToLower(), cancellationToken);

        if (skuExists)
        {
            throw new InvalidOperationException($"SKU '{sku}' already exists in the catalog.");
        }

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        var variant = new ProductVariant
        {
            ProductId = productId,
            Sku = sku,
            Name = request.Name.Trim(),
            Size = request.Size?.Trim(),
            Colour = request.Colour?.Trim(),
            Price = request.Price,
            IsActive = request.IsActive
        };

        var initialStock = Math.Max(0, request.InitialStock);
        var inventory = new Inventory
        {
            ProductVariantId = variant.Id,
            ProductVariant = variant,
            QuantityOnHand = initialStock,
            ReservedQuantity = 0,
            ReorderLevel = Math.Max(0, request.ReorderLevel)
        };

        variant.Inventory = inventory;
        _context.ProductVariants.Add(variant);
        _context.Inventory.Add(inventory);

        if (initialStock > 0)
        {
            _context.InventoryTransactions.Add(new InventoryTransaction
            {
                ProductVariantId = variant.Id,
                Type = InventoryTransactionType.Receipt,
                QuantityChange = initialStock,
                QuantityOnHandAfter = initialStock,
                Reference = "INITIAL-STOCK",
                Note = "Initial stock allocated on variant creation."
            });
        }

        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new ProductVariantResponse
        {
            Id = variant.Id,
            ProductId = variant.ProductId,
            Sku = variant.Sku,
            Name = variant.Name,
            Size = variant.Size,
            Colour = variant.Colour,
            Price = variant.Price,
            IsActive = variant.IsActive,
            QuantityOnHand = inventory.QuantityOnHand,
            ReservedQuantity = inventory.ReservedQuantity,
            AvailableQuantity = inventory.QuantityOnHand,
            ReorderLevel = inventory.ReorderLevel,
            IsLowStock = inventory.QuantityOnHand <= inventory.ReorderLevel
        };
    }

    public async Task<ProductVariantResponse?> UpdateVariantAsync(
        Guid productId,
        Guid variantId,
        UpdateProductVariantRequest request,
        CancellationToken cancellationToken = default)
    {
        var variant = await _context.ProductVariants
            .Include(v => v.Inventory)
            .FirstOrDefaultAsync(v => v.Id == variantId && v.ProductId == productId, cancellationToken);

        if (variant is null)
        {
            return null;
        }

        var sku = request.Sku.Trim();
        var skuDuplicate = await _context.ProductVariants
            .AnyAsync(v => v.Id != variantId && v.Sku.ToLower() == sku.ToLower(), cancellationToken);

        if (skuDuplicate)
        {
            throw new InvalidOperationException($"Another variant with SKU '{sku}' already exists.");
        }

        variant.Sku = sku;
        variant.Name = request.Name.Trim();
        variant.Size = request.Size?.Trim();
        variant.Colour = request.Colour?.Trim();
        variant.Price = request.Price;
        variant.IsActive = request.IsActive;

        if (variant.Inventory != null)
        {
            variant.Inventory.ReorderLevel = Math.Max(0, request.ReorderLevel);
        }

        await _context.SaveChangesAsync(cancellationToken);

        return new ProductVariantResponse
        {
            Id = variant.Id,
            ProductId = variant.ProductId,
            Sku = variant.Sku,
            Name = variant.Name,
            Size = variant.Size,
            Colour = variant.Colour,
            Price = variant.Price,
            IsActive = variant.IsActive,
            QuantityOnHand = variant.Inventory?.QuantityOnHand ?? 0,
            ReservedQuantity = variant.Inventory?.ReservedQuantity ?? 0,
            AvailableQuantity = Math.Max(0, (variant.Inventory?.QuantityOnHand ?? 0) - (variant.Inventory?.ReservedQuantity ?? 0)),
            ReorderLevel = variant.Inventory?.ReorderLevel ?? 0,
            IsLowStock = (variant.Inventory?.QuantityOnHand ?? 0) <= (variant.Inventory?.ReorderLevel ?? 0)
        };
    }

    public async Task<bool> DeleteVariantAsync(
        Guid productId,
        Guid variantId,
        CancellationToken cancellationToken = default)
    {
        var variant = await _context.ProductVariants
            .FirstOrDefaultAsync(v => v.Id == variantId && v.ProductId == productId, cancellationToken);

        if (variant is null)
        {
            return false;
        }

        var hasOrders = await _context.OrderItems
            .AnyAsync(oi => oi.ProductVariantId == variantId, cancellationToken);

        if (hasOrders)
        {
            variant.IsActive = false;
        }
        else
        {
            _context.ProductVariants.Remove(variant);
        }

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    #endregion

    #region Images

    public async Task<ProductImageResponse> AddImageAsync(
        Guid productId,
        CreateProductImageRequest request,
        CancellationToken cancellationToken = default)
    {
        var product = await _context.Products.FindAsync(new object[] { productId }, cancellationToken);

        if (product is null)
        {
            throw new InvalidOperationException($"Product with ID {productId} does not exist.");
        }

        var existingCount = await _context.ProductImages
            .CountAsync(i => i.ProductId == productId, cancellationToken);

        var isPrimary = request.IsPrimary || existingCount == 0;

        if (isPrimary)
        {
            var existingPrimaries = await _context.ProductImages
                .Where(i => i.ProductId == productId && i.IsPrimary)
                .ToListAsync(cancellationToken);

            foreach (var p in existingPrimaries)
            {
                p.IsPrimary = false;
            }
        }

        var image = new ProductImage
        {
            ProductId = productId,
            ImageUrl = request.ImageUrl.Trim(),
            AltText = request.AltText?.Trim(),
            IsPrimary = isPrimary,
            DisplayOrder = request.DisplayOrder
        };

        _context.ProductImages.Add(image);
        await _context.SaveChangesAsync(cancellationToken);

        return new ProductImageResponse
        {
            Id = image.Id,
            ProductId = image.ProductId,
            ImageUrl = image.ImageUrl,
            AltText = image.AltText,
            IsPrimary = image.IsPrimary,
            DisplayOrder = image.DisplayOrder,
            CreatedAt = image.CreatedAt
        };
    }

    public async Task<ProductImageResponse?> UpdateImageAsync(
        Guid productId,
        Guid imageId,
        UpdateProductImageRequest request,
        CancellationToken cancellationToken = default)
    {
        var image = await _context.ProductImages
            .FirstOrDefaultAsync(i => i.Id == imageId && i.ProductId == productId, cancellationToken);

        if (image is null)
        {
            return null;
        }

        if (request.IsPrimary && !image.IsPrimary)
        {
            var existingPrimaries = await _context.ProductImages
                .Where(i => i.ProductId == productId && i.IsPrimary)
                .ToListAsync(cancellationToken);

            foreach (var p in existingPrimaries)
            {
                p.IsPrimary = false;
            }
        }

        image.ImageUrl = request.ImageUrl.Trim();
        image.AltText = request.AltText?.Trim();
        image.IsPrimary = request.IsPrimary;
        image.DisplayOrder = request.DisplayOrder;

        await _context.SaveChangesAsync(cancellationToken);

        return new ProductImageResponse
        {
            Id = image.Id,
            ProductId = image.ProductId,
            ImageUrl = image.ImageUrl,
            AltText = image.AltText,
            IsPrimary = image.IsPrimary,
            DisplayOrder = image.DisplayOrder,
            CreatedAt = image.CreatedAt
        };
    }

    public async Task<bool> DeleteImageAsync(
        Guid productId,
        Guid imageId,
        CancellationToken cancellationToken = default)
    {
        var image = await _context.ProductImages
            .FirstOrDefaultAsync(i => i.Id == imageId && i.ProductId == productId, cancellationToken);

        if (image is null)
        {
            return false;
        }

        var wasPrimary = image.IsPrimary;
        _context.ProductImages.Remove(image);

        if (wasPrimary)
        {
            var nextImage = await _context.ProductImages
                .Where(i => i.ProductId == productId && i.Id != imageId)
                .OrderBy(i => i.DisplayOrder)
                .FirstOrDefaultAsync(cancellationToken);

            if (nextImage != null)
            {
                nextImage.IsPrimary = true;
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> SetPrimaryImageAsync(
        Guid productId,
        Guid imageId,
        CancellationToken cancellationToken = default)
    {
        var targetImage = await _context.ProductImages
            .FirstOrDefaultAsync(i => i.Id == imageId && i.ProductId == productId, cancellationToken);

        if (targetImage is null)
        {
            return false;
        }

        var currentPrimaries = await _context.ProductImages
            .Where(i => i.ProductId == productId && i.IsPrimary)
            .ToListAsync(cancellationToken);

        foreach (var p in currentPrimaries)
        {
            p.IsPrimary = false;
        }

        targetImage.IsPrimary = true;
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    #endregion

    private static ProductDetailResponse MapToDetailResponse(Product product)
    {
        return new ProductDetailResponse
        {
            Id = product.Id,
            Name = product.Name,
            Description = product.Description,
            SupplierId = product.SupplierId,
            SupplierName = product.Supplier != null ? product.Supplier.Name : null,
            IsActive = product.IsActive,
            Categories = product.ProductCategories.Select(pc => new CategoryResponse
            {
                Id = pc.Category.Id,
                Name = pc.Category.Name,
                Description = pc.Category.Description,
                IsActive = pc.Category.IsActive,
                CreatedAt = pc.Category.CreatedAt,
                UpdatedAt = pc.Category.UpdatedAt
            }).ToList(),
            Variants = product.Variants.OrderBy(v => v.Price).Select(v => new ProductVariantResponse
            {
                Id = v.Id,
                ProductId = v.ProductId,
                Sku = v.Sku,
                Name = v.Name,
                Size = v.Size,
                Colour = v.Colour,
                Price = v.Price,
                IsActive = v.IsActive,
                QuantityOnHand = v.Inventory?.QuantityOnHand ?? 0,
                ReservedQuantity = v.Inventory?.ReservedQuantity ?? 0,
                AvailableQuantity = Math.Max(0, (v.Inventory?.QuantityOnHand ?? 0) - (v.Inventory?.ReservedQuantity ?? 0)),
                ReorderLevel = v.Inventory?.ReorderLevel ?? 0,
                IsLowStock = (v.Inventory?.QuantityOnHand ?? 0) <= (v.Inventory?.ReorderLevel ?? 0)
            }).ToList(),
            Images = product.Images
                .OrderByDescending(i => i.IsPrimary)
                .ThenBy(i => i.DisplayOrder)
                .Select(i => new ProductImageResponse
                {
                    Id = i.Id,
                    ProductId = i.ProductId,
                    ImageUrl = i.ImageUrl,
                    AltText = i.AltText,
                    IsPrimary = i.IsPrimary,
                    DisplayOrder = i.DisplayOrder,
                    CreatedAt = i.CreatedAt
                }).ToList(),
            CreatedAt = product.CreatedAt,
            UpdatedAt = product.UpdatedAt
        };
    }
}
