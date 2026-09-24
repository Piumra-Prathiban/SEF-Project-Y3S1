using Microsoft.EntityFrameworkCore;
using SEF_Project.Api.Data;

namespace SEF_Project.Api.Services.Shopping;

public class ProductAvailabilityService : IProductAvailabilityService
{
    private const int MaximumVariantsPerRequest = 50;
    private readonly AppDbContext _context;

    public ProductAvailabilityService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<AvailableProductVariant>> GetAvailableVariantsAsync(
        IReadOnlyCollection<Guid> variantIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(variantIds);

        var distinctIds = variantIds.Distinct().ToList();

        if (distinctIds.Count == 0 || distinctIds.Count > MaximumVariantsPerRequest)
        {
            throw new ArgumentException(
                $"Between 1 and {MaximumVariantsPerRequest} variant identifiers are required.");
        }

        if (distinctIds.Any(id => id == Guid.Empty))
        {
            throw new ArgumentException("Variant identifiers cannot be empty.");
        }

        var variants = await _context.ProductVariants
            .AsNoTracking()
            .Include(variant => variant.Product)
            .Include(variant => variant.Inventory)
            .Where(variant =>
                distinctIds.Contains(variant.Id) &&
                variant.IsActive &&
                variant.Product.IsActive &&
                variant.Inventory != null &&
                variant.Inventory.QuantityOnHand -
                    variant.Inventory.ReservedQuantity > 0)
            .ToListAsync(cancellationToken);

        return variants
            .Select(variant => new AvailableProductVariant(
                variant.ProductId,
                variant.Id,
                variant.Product.Name,
                variant.Name,
                variant.Sku,
                variant.Price,
                variant.Inventory!.QuantityOnHand -
                    variant.Inventory.ReservedQuantity,
                Size: null,
                Colour: null))
            .OrderBy(variant => distinctIds.IndexOf(variant.VariantId))
            .ToList();
    }
}
