using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SEF_Project.Api.Data;

namespace SEF_Project.Api.Services.AgenticAI;

public class InventoryAgentToolRegistry : IInventoryAgentToolRegistry
{
    private readonly AppDbContext _context;

    public InventoryAgentToolRegistry(AppDbContext context)
    {
        _context = context;
    }

    public bool IsAllowed(string toolName) =>
        InventoryAgentConstants.AllowedTools.Contains(toolName);

    public async Task<JsonDocument> ExecuteAsync(
        string toolName,
        JsonDocument arguments,
        CancellationToken cancellationToken = default)
    {
        if (!IsAllowed(toolName))
        {
            throw new UnauthorizedAccessException(
                $"Tool '{toolName}' is not allowed for the Inventory Analysis Agent.");
        }

        return toolName switch
        {
            "GetProductInventory" => await GetProductInventoryAsync(
                arguments,
                cancellationToken),
            "GetLowStockProducts" => await GetLowStockProductsAsync(
                cancellationToken),
            "GetStockHistory" => await GetStockHistoryAsync(
                arguments,
                cancellationToken),
            "GetProductDetails" => await GetProductDetailsAsync(
                arguments,
                cancellationToken),
            _ => throw new UnauthorizedAccessException(
                $"Tool '{toolName}' is not allowed for the Inventory Analysis Agent.")
        };
    }

    private async Task<JsonDocument> GetProductInventoryAsync(
        JsonDocument arguments,
        CancellationToken cancellationToken)
    {
        var productIds = ReadGuidList(arguments, "productIds");
        var variantIds = ReadGuidList(arguments, "variantIds");

        var query = _context.Inventory
            .AsNoTracking()
            .Include(i => i.ProductVariant)
                .ThenInclude(v => v.Product)
            .AsQueryable();

        if (productIds.Count > 0)
        {
            query = query.Where(
                i => productIds.Contains(i.ProductVariant.ProductId));
        }

        if (variantIds.Count > 0)
        {
            query = query.Where(i => variantIds.Contains(i.ProductVariantId));
        }

        var result = await query
            .OrderBy(i => i.ProductVariant.Product.Name)
            .ThenBy(i => i.ProductVariant.Sku)
            .Select(i => new
            {
                variantId = i.ProductVariantId,
                sku = i.ProductVariant.Sku,
                productId = i.ProductVariant.ProductId,
                productName = i.ProductVariant.Product.Name,
                currentStock = i.QuantityOnHand,
                reservedQuantity = i.ReservedQuantity,
                reorderLevel = i.ReorderLevel
            })
            .ToListAsync(cancellationToken);

        return ToJsonDocument(new { inventory = result });
    }

    private async Task<JsonDocument> GetLowStockProductsAsync(
        CancellationToken cancellationToken)
    {
        var result = await _context.Inventory
            .AsNoTracking()
            .Include(i => i.ProductVariant)
                .ThenInclude(v => v.Product)
            .Where(i => i.QuantityOnHand <= i.ReorderLevel)
            .OrderBy(i => i.QuantityOnHand)
            .ThenBy(i => i.ProductVariant.Sku)
            .Select(i => new
            {
                variantId = i.ProductVariantId,
                sku = i.ProductVariant.Sku,
                productId = i.ProductVariant.ProductId,
                productName = i.ProductVariant.Product.Name,
                currentStock = i.QuantityOnHand,
                reservedQuantity = i.ReservedQuantity,
                reorderLevel = i.ReorderLevel
            })
            .ToListAsync(cancellationToken);

        return ToJsonDocument(new { inventory = result });
    }

    private async Task<JsonDocument> GetStockHistoryAsync(
        JsonDocument arguments,
        CancellationToken cancellationToken)
    {
        var variantIds = ReadGuidList(arguments, "variantIds");

        if (variantIds.Count == 0)
        {
            throw new ArgumentException(
                "GetStockHistory requires at least one variantId.");
        }

        var result = await _context.InventoryTransactions
            .AsNoTracking()
            .Where(t => variantIds.Contains(t.ProductVariantId))
            .OrderByDescending(t => t.CreatedAt)
            .Take(100)
            .Select(t => new
            {
                t.ProductVariantId,
                type = t.Type.ToString(),
                quantityChange = t.QuantityChange,
                previousQuantity = t.QuantityOnHandBefore,
                newQuantity = t.QuantityOnHandAfter,
                reason = t.Note,
                timestamp = t.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return ToJsonDocument(new { transactions = result });
    }

    private async Task<JsonDocument> GetProductDetailsAsync(
        JsonDocument arguments,
        CancellationToken cancellationToken)
    {
        var productIds = ReadGuidList(arguments, "productIds");
        var variantIds = ReadGuidList(arguments, "variantIds");

        var query = _context.ProductVariants
            .AsNoTracking()
            .Include(v => v.Product)
                .ThenInclude(p => p.Category)
            .Include(v => v.Product)
                .ThenInclude(p => p.Collection)
            .Include(v => v.Size)
            .Include(v => v.Colour)
            .AsQueryable();

        if (productIds.Count > 0)
        {
            query = query.Where(v => productIds.Contains(v.ProductId));
        }

        if (variantIds.Count > 0)
        {
            query = query.Where(v => variantIds.Contains(v.Id));
        }

        var result = await query
            .OrderBy(v => v.Product.Name)
            .ThenBy(v => v.Sku)
            .Select(v => new
            {
                variantId = v.Id,
                v.Sku,
                variantName = v.Name,
                v.Price,
                productId = v.ProductId,
                productName = v.Product.Name,
                category = v.Product.Category.Name,
                collection = v.Product.Collection.Name,
                size = v.Size.Name,
                colour = v.Colour.Name,
                isActive = v.IsActive
            })
            .ToListAsync(cancellationToken);

        return ToJsonDocument(new { variants = result });
    }

    private static List<Guid> ReadGuidList(
        JsonDocument arguments,
        string propertyName)
    {
        if (!arguments.RootElement.TryGetProperty(
                propertyName,
                out var element)
            || element.ValueKind is JsonValueKind.Null
                or JsonValueKind.Undefined)
        {
            return new List<Guid>();
        }

        if (element.ValueKind != JsonValueKind.Array)
        {
            throw new ArgumentException(
                $"Tool argument '{propertyName}' must be an array of GUID values.");
        }

        var values = new List<Guid>();

        foreach (var item in element.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String
                || !Guid.TryParse(item.GetString(), out var value))
            {
                throw new ArgumentException(
                    $"Tool argument '{propertyName}' contains an invalid GUID value.");
            }

            if (value != Guid.Empty)
            {
                values.Add(value);
            }
        }

        return values.Distinct().ToList();
    }

    private static JsonDocument ToJsonDocument<T>(T value) =>
        JsonDocument.Parse(JsonSerializer.Serialize(value));
}
