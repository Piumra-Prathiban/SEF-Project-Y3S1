using Microsoft.EntityFrameworkCore;
using SEF_Project.Api.Data;
using SEF_Project.Api.DTOs.Shopping;
using SEF_Project.Api.Models.Catalog;

namespace SEF_Project.Api.Services.Shopping;

public class ProductSearchService : IProductSearchService
{
    private static readonly HashSet<string> SupportedSortFields =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "name",
            "price",
            "newest"
        };

    private readonly AppDbContext _context;

    public ProductSearchService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<PagedProductResponse> SearchAsync(
        ProductSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ValidateQuery(query);

        var products = BuildFilteredQuery(query);
        var totalCount = await products.CountAsync(cancellationToken);
        var orderedProducts = ApplySorting(products, query);

        var pageItems = await orderedProducts
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .AsSplitQuery()
            .Include(product => product.Category)
            .Include(product => product.Variants)
                .ThenInclude(variant => variant.InventoryStock)
            .ToListAsync(cancellationToken);

        return new PagedProductResponse
        {
            Items = pageItems.Select(MapProduct).ToList(),
            Page = query.Page,
            PageSize = query.PageSize,
            TotalCount = totalCount,
            TotalPages = totalCount == 0
                ? 0
                : (totalCount + query.PageSize - 1) / query.PageSize
        };
    }

    private IQueryable<Product> BuildFilteredQuery(ProductSearchQuery query)
    {
        var products = _context.Products
            .AsNoTracking()
            .Where(product =>
                product.IsActive &&
                product.Variants.Any(variant => variant.IsActive));

        var keyword = query.Search?.Trim().ToLower();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            products = products.Where(product =>
                product.Name.ToLower().Contains(keyword) ||
                (product.Description != null &&
                    product.Description.ToLower().Contains(keyword)) ||
                product.Variants.Any(variant =>
                    variant.IsActive &&
                    (variant.Name.ToLower().Contains(keyword) ||
                        variant.Sku.ToLower().Contains(keyword))));
        }

        if (query.CategoryId.HasValue)
        {
            products = products.Where(product =>
                product.CategoryId == query.CategoryId.Value &&
                product.Category.IsActive);
        }

        if (query.MinPrice.HasValue || query.MaxPrice.HasValue || query.InStockOnly)
        {
            products = products.Where(product =>
                product.Variants.Any(variant =>
                    variant.IsActive &&
                    (!query.MinPrice.HasValue ||
                        variant.Price >= query.MinPrice.Value) &&
                    (!query.MaxPrice.HasValue ||
                        variant.Price <= query.MaxPrice.Value) &&
                    (!query.InStockOnly ||
                        (variant.InventoryStock != null &&
                            variant.InventoryStock.QuantityOnHand -
                                variant.InventoryStock.ReservedQuantity > 0))));
        }

        return products;
    }

    private static IOrderedQueryable<Product> ApplySorting(
        IQueryable<Product> products,
        ProductSearchQuery query)
    {
        var descending = string.Equals(
            query.SortDirection,
            "desc",
            StringComparison.OrdinalIgnoreCase);

        IOrderedQueryable<Product> ordered = query.SortBy.ToLowerInvariant() switch
        {
            "price" when descending => products.OrderByDescending(product =>
                product.Variants
                    .Where(variant => variant.IsActive)
                    .Min(variant => (double)variant.Price)),
            "price" => products.OrderBy(product =>
                product.Variants
                    .Where(variant => variant.IsActive)
                    .Min(variant => (double)variant.Price)),
            "newest" when descending =>
                products.OrderByDescending(product => product.CreatedAt),
            "newest" => products.OrderBy(product => product.CreatedAt),
            "name" when descending =>
                products.OrderByDescending(product => product.Name),
            _ => products.OrderBy(product => product.Name)
        };

        return ordered.ThenBy(product => product.Id);
    }

    private static ShoppingProductResponse MapProduct(Product product)
    {
        var variants = product.Variants
            .Where(variant => variant.IsActive)
            .OrderBy(variant => variant.Price)
            .ThenBy(variant => variant.Id)
            .Select(variant =>
            {
                var availableQuantity = variant.InventoryStock == null
                    ? 0
                    : Math.Max(
                        0,
                        variant.InventoryStock.QuantityOnHand -
                            variant.InventoryStock.ReservedQuantity);

                return new ShoppingProductVariantResponse
                {
                    Id = variant.Id,
                    Sku = variant.Sku,
                    Name = variant.Name,
                    Price = variant.Price,
                    AvailableQuantity = availableQuantity,
                    IsAvailable = availableQuantity > 0
                };
            })
            .ToList();

        return new ShoppingProductResponse
        {
            Id = product.Id,
            Name = product.Name,
            Description = product.Description,
            MinimumPrice = variants.Min(variant => variant.Price),
            IsAvailable = variants.Any(variant => variant.IsAvailable),
            Categories = product.Category is { IsActive: true }
                ? new[]
                {
                    new ShoppingCategoryResponse
                    {
                        Id = product.CategoryId,
                        Name = product.Category.Name
                    }
                }
                : Array.Empty<ShoppingCategoryResponse>(),
            Variants = variants
        };
    }

    private static void ValidateQuery(ProductSearchQuery query)
    {
        if (query.Page is < 1 or > 10000)
        {
            throw new ArgumentException("Page must be between 1 and 10000.");
        }

        if (query.PageSize is < 1 or > 100)
        {
            throw new ArgumentException("Page size must be between 1 and 100.");
        }

        if (query.MinPrice < 0 || query.MaxPrice < 0)
        {
            throw new ArgumentException("Prices cannot be negative.");
        }

        if (query.MinPrice.HasValue &&
            query.MaxPrice.HasValue &&
            query.MinPrice.Value > query.MaxPrice.Value)
        {
            throw new ArgumentException(
                "Minimum price cannot be greater than maximum price.");
        }

        if (string.IsNullOrWhiteSpace(query.SortBy) ||
            !SupportedSortFields.Contains(query.SortBy))
        {
            throw new ArgumentException(
                "SortBy must be one of: name, price, newest.");
        }

        if (!string.Equals(query.SortDirection, "asc", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(query.SortDirection, "desc", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("SortDirection must be asc or desc.");
        }

        if (query.Search?.Length > 100)
        {
            throw new ArgumentException("Search cannot exceed 100 characters.");
        }
    }
}
