using System.Data;
using Microsoft.EntityFrameworkCore;
using SEF_Project.Api.Data;
using SEF_Project.Api.DTOs.Catalog;
using SEF_Project.Api.Models.Catalog;
using SEF_Project.Api.Models.Enums;

namespace SEF_Project.Api.Services.Catalog;

public class InventoryService : IInventoryService
{
    private readonly AppDbContext _context;

    public InventoryService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<InventoryResponseDto>> GetInventoryAsync(
        CancellationToken cancellationToken = default)
    {
        var inventory = await InventoryQuery()
            .AsNoTracking()
            .OrderBy(i => i.ProductVariant.Product.Name)
            .ThenBy(i => i.ProductVariant.Sku)
            .ToListAsync(cancellationToken);

        return inventory.Select(MapInventory).ToList();
    }

    public async Task<InventoryResponseDto?> GetInventoryByVariantIdAsync(
        Guid productVariantId,
        CancellationToken cancellationToken = default)
    {
        var inventory = await InventoryQuery()
            .AsNoTracking()
            .FirstOrDefaultAsync(
                i => i.ProductVariantId == productVariantId,
                cancellationToken);

        return inventory is null ? null : MapInventory(inventory);
    }

    public async Task<List<InventoryResponseDto>> GetLowStockAsync(
        CancellationToken cancellationToken = default)
    {
        var inventory = await InventoryQuery()
            .AsNoTracking()
            .Where(i => i.QuantityOnHand - i.ReservedQuantity <= i.ReorderLevel)
            .OrderBy(i => i.QuantityOnHand - i.ReservedQuantity)
            .ThenBy(i => i.ProductVariant.Sku)
            .ToListAsync(cancellationToken);

        return inventory.Select(MapInventory).ToList();
    }

    public async Task<InventoryResponseDto?> AdjustStockAsync(
        StockAdjustmentDto request,
        int? performedByUserId = null,
        CancellationToken cancellationToken = default)
    {
        if (request.Quantity <= 0)
        {
            throw new ArgumentException("Quantity must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            throw new ArgumentException("Reason is required.");
        }

        var quantityChange = CalculateQuantityChange(request.Type, request.Quantity);

        await using var transaction =
            await _context.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

        try
        {
            var inventory = await InventoryQuery()
                .FirstOrDefaultAsync(
                    i => i.ProductVariantId == request.ProductVariantId,
                    cancellationToken);

            if (inventory is null)
            {
                return null;
            }

            var quantityBefore = inventory.QuantityOnHand;
            var quantityAfter = quantityBefore + quantityChange;

            if (quantityAfter < 0)
            {
                throw new InvalidOperationException(
                    "Stock adjustment cannot make quantity on hand negative.");
            }

            if (quantityAfter < inventory.ReservedQuantity)
            {
                throw new InvalidOperationException(
                    "Stock adjustment cannot reduce stock below reserved quantity.");
            }

            inventory.QuantityOnHand = quantityAfter;

            _context.InventoryTransactions.Add(new StockTransaction
            {
                ProductVariantId = request.ProductVariantId,
                Type = request.Type,
                QuantityChange = quantityChange,
                QuantityOnHandBefore = quantityBefore,
                QuantityOnHandAfter = quantityAfter,
                PerformedByUserId = performedByUserId,
                Reference = NullIfWhitespace(request.Reference),
                Note = request.Reason.Trim()
            });

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return MapInventory(inventory);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<List<StockTransactionResponseDto>> GetStockTransactionsAsync(
        Guid? productVariantId = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.InventoryTransactions
            .AsNoTracking()
            .Include(t => t.ProductVariant)
            .Include(t => t.PerformedByUser)
            .AsQueryable();

        if (productVariantId is not null)
        {
            query = query.Where(t => t.ProductVariantId == productVariantId.Value);
        }

        var transactions = await query
            .OrderByDescending(t => t.CreatedAt)
            .ThenByDescending(t => t.Id)
            .ToListAsync(cancellationToken);

        return transactions.Select(MapTransaction).ToList();
    }

    private IQueryable<InventoryStock> InventoryQuery() =>
        _context.Inventory
            .Include(i => i.ProductVariant)
                .ThenInclude(v => v.Product);

    public static InventoryResponseDto MapInventory(InventoryStock inventory)
    {
        var availableQuantity =
            inventory.QuantityOnHand - inventory.ReservedQuantity;

        return new InventoryResponseDto
        {
            Id = inventory.Id,
            ProductVariantId = inventory.ProductVariantId,
            Sku = inventory.ProductVariant?.Sku ?? string.Empty,
            ProductName = inventory.ProductVariant?.Product?.Name ?? string.Empty,
            VariantName = inventory.ProductVariant?.Name ?? string.Empty,
            QuantityOnHand = inventory.QuantityOnHand,
            ReservedQuantity = inventory.ReservedQuantity,
            AvailableQuantity = availableQuantity,
            ReorderLevel = inventory.ReorderLevel,
            IsLowStock = availableQuantity <= inventory.ReorderLevel,
            CreatedAt = inventory.CreatedAt,
            UpdatedAt = inventory.UpdatedAt
        };
    }

    private static StockTransactionResponseDto MapTransaction(
        StockTransaction transaction) =>
        new()
        {
            Id = transaction.Id,
            ProductVariantId = transaction.ProductVariantId,
            Sku = transaction.ProductVariant?.Sku ?? string.Empty,
            Type = transaction.Type,
            Quantity = Math.Abs(transaction.QuantityChange),
            QuantityChange = transaction.QuantityChange,
            PreviousQuantityOnHand = transaction.QuantityOnHandBefore,
            QuantityOnHandAfter = transaction.QuantityOnHandAfter,
            PerformedByUserId = transaction.PerformedByUserId,
            PerformedByUserEmail = transaction.PerformedByUser?.Email,
            Reference = transaction.Reference,
            Reason = transaction.Note,
            CreatedAt = transaction.CreatedAt
        };

    private static int CalculateQuantityChange(
        InventoryTransactionType type,
        int quantity) =>
        type switch
        {
            InventoryTransactionType.StockIn => quantity,
            InventoryTransactionType.StockOut => -quantity,
            InventoryTransactionType.Adjustment => quantity,
            _ => throw new ArgumentException(
                "Only StockIn, StockOut and Adjustment transactions can be created through manual stock adjustment.")
        };

    private static string? NullIfWhitespace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
