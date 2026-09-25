using Microsoft.EntityFrameworkCore;
using SEF_Project.Api.Data;
using SEF_Project.Api.DTOs.Storefront;
using SEF_Project.Api.Models.Catalog;

namespace SEF_Project.Api.Services.Storefront;

/// <summary>
/// Read-only projections for the public storefront. Nothing here mutates data,
/// and only active products/variants are exposed.
/// </summary>
public class StorefrontService : IStorefrontService
{
    private const int DefaultLimit = 24;
    private const int MaxLimit = 60;

    private readonly AppDbContext _context;

    public StorefrontService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<StorefrontProductResponseDto>> GetProductsAsync(
        StorefrontProductQueryDto query,
        CancellationToken cancellationToken = default)
    {
        var take = Math.Clamp(
            query.Limit <= 0 ? DefaultLimit : query.Limit,
            1,
            MaxLimit);

        var productsQuery = StorefrontQuery();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLower();
            productsQuery = productsQuery.Where(p => p.Name.ToLower().Contains(term));
        }

        if (query.CategoryId is not null)
        {
            productsQuery = productsQuery.Where(
                p => p.CategoryId == query.CategoryId.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Size))
        {
            var size = query.Size.Trim();

            productsQuery = productsQuery.Where(
                p => p.Variants.Any(
                    v => v.IsActive && v.Size != null && v.Size.Name == size));
        }

        if (!string.IsNullOrWhiteSpace(query.Colour))
        {
            var colour = query.Colour.Trim();

            productsQuery = productsQuery.Where(
                p => p.Variants.Any(
                    v => v.IsActive && v.Colour != null && v.Colour.Name == colour));
        }

        if (query.MinPrice is not null)
        {
            productsQuery = productsQuery.Where(
                p => p.Variants.Any(
                    v => v.IsActive && v.Price >= query.MinPrice.Value));
        }

        if (query.MaxPrice is not null)
        {
            productsQuery = productsQuery.Where(
                p => p.Variants.Any(
                    v => v.IsActive && v.Price <= query.MaxPrice.Value));
        }

        var descending = string.Equals(
            query.SortDirection,
            "desc",
            StringComparison.OrdinalIgnoreCase);

        var sorted = query.SortBy?.Trim().ToLowerInvariant() switch
        {
            "price" => descending
                ? productsQuery.OrderByDescending(
                    p => p.Variants.Select(v => (double?)v.Price).Min() ?? 0)
                : productsQuery.OrderBy(
                    p => p.Variants.Select(v => (double?)v.Price).Min() ?? 0),
            _ => descending
                ? productsQuery.OrderByDescending(p => p.Name)
                : productsQuery.OrderBy(p => p.Name)
        };

        var products = await sorted
            .Take(take)
            .ToListAsync(cancellationToken);

        return products.Select(Map).ToList();
    }

    public async Task<StorefrontProductResponseDto?> GetProductByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var product = await StorefrontQuery()
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        return product is null ? null : Map(product);
    }

    private IQueryable<Product> StorefrontQuery() =>
        _context.Products
            .AsNoTracking()
            .Where(p => p.IsActive)
            .Where(p => p.Variants.Any(v => v.IsActive))
            .Include(p => p.Category)
            .Include(p => p.Collection)
            .Include(p => p.Variants)
                .ThenInclude(v => v.Size)
            .Include(p => p.Variants)
                .ThenInclude(v => v.Colour)
            .Include(p => p.Variants)
                .ThenInclude(v => v.InventoryStock);

    private static StorefrontProductResponseDto Map(Product product)
    {
        var variants = product.Variants
            .Where(v => v.IsActive)
            .ToList();

        var prices = variants
            .Select(v => v.Price)
            .ToList();

        return new StorefrontProductResponseDto
        {
            Id = product.Id,
            Name = product.Name,
            Description = product.Description,
            ImageUrl = product.ImageUrl,
            CategoryId = product.CategoryId,
            CategoryName = product.Category?.Name ?? string.Empty,
            CollectionName = product.Collection?.Name ?? string.Empty,
            PriceFrom = prices.Count == 0 ? 0m : prices.Min(),
            PriceTo = prices.Count == 0 ? 0m : prices.Max(),
            Sizes = variants
                .Where(v => v.Size is not null)
                .OrderBy(v => v.Size!.DisplayOrder)
                .Select(v => v.Size!.Name)
                .Distinct()
                .ToList(),
            Colours = variants
                .Where(v => v.Colour is not null)
                .GroupBy(v => v.Colour!.Name)
                .Select(group => new StorefrontColourResponseDto
                {
                    Name = group.Key,
                    HexCode = group.Select(v => v.Colour!.HexCode).FirstOrDefault()
                })
                .OrderBy(colour => colour.Name)
                .ToList(),
            Variants = variants
                .OrderBy(v => v.Size?.DisplayOrder ?? 0)
                .ThenBy(v => v.Colour?.Name)
                .Select(v => new StorefrontVariantResponseDto
                {
                    Id = v.Id,
                    SizeName = v.Size?.Name ?? string.Empty,
                    ColourName = v.Colour?.Name ?? string.Empty,
                    ColourHex = v.Colour?.HexCode,
                    Price = v.Price,
                    InStock = v.InventoryStock is not null
                        && v.InventoryStock.QuantityOnHand
                            > v.InventoryStock.ReservedQuantity
                })
                .ToList(),
            InStock = variants.Any(v =>
                v.InventoryStock is not null
                && v.InventoryStock.QuantityOnHand
                    > v.InventoryStock.ReservedQuantity)
        };
    }
}
