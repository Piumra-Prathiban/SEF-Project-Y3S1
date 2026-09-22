using Microsoft.EntityFrameworkCore;
using SEF_Project.Api.Data;
using SEF_Project.Api.DTOs.Inventory;
using SEF_Project.Api.Models.Catalog;

namespace SEF_Project.Api.Services.InventoryManagement;

public class InventoryService : IInventoryService
{
    private readonly AppDbContext _context;

    public InventoryService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<InventoryListResponse> GetInventoryAsync(
        InventoryQuery query,
        CancellationToken cancellationToken = default)
    {
        var inventoryQuery = _context.Inventory
            .AsNoTracking()
            .Include(i => i.ProductVariant)
                .ThenInclude(v => v.Product)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToLower();
            inventoryQuery = inventoryQuery.Where(i =>
                i.ProductVariant.Sku.ToLower().Contains(search) ||
                i.ProductVariant.Name.ToLower().Contains(search) ||
                i.ProductVariant.Product.Name.ToLower().Contains(search));
        }

        if (query.LowStockOnly == true)
        {
            inventoryQuery = inventoryQuery.Where(i => i.QuantityOnHand <= i.ReorderLevel);
        }

        var totalCount = await inventoryQuery.CountAsync(cancellationToken);

        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var items = await inventoryQuery
            .OrderBy(i => i.ProductVariant.Product.Name)
            .ThenBy(i => i.ProductVariant.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(i => new InventoryResponse
            {
                Id = i.Id,
                ProductVariantId = i.ProductVariantId,
                ProductId = i.ProductVariant.ProductId,
                ProductName = i.ProductVariant.Product.Name,
                VariantName = i.ProductVariant.Name,
                Sku = i.ProductVariant.Sku,
                Size = i.ProductVariant.Size,
                Colour = i.ProductVariant.Colour,
                Price = i.ProductVariant.Price,
                QuantityOnHand = i.QuantityOnHand,
                ReservedQuantity = i.ReservedQuantity,
                AvailableQuantity = i.QuantityOnHand - i.ReservedQuantity,
                ReorderLevel = i.ReorderLevel,
                IsLowStock = i.QuantityOnHand <= i.ReorderLevel,
                LastUpdatedAt = i.UpdatedAt
            })
            .ToListAsync(cancellationToken);

        return new InventoryListResponse
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<InventoryResponse?> GetVariantInventoryAsync(
        Guid variantId,
        CancellationToken cancellationToken = default)
    {
        var inventory = await _context.Inventory
            .AsNoTracking()
            .Include(i => i.ProductVariant)
                .ThenInclude(v => v.Product)
            .FirstOrDefaultAsync(i => i.ProductVariantId == variantId, cancellationToken);

        if (inventory is null)
        {
            return null;
        }

        return new InventoryResponse
        {
            Id = inventory.Id,
            ProductVariantId = inventory.ProductVariantId,
            ProductId = inventory.ProductVariant.ProductId,
            ProductName = inventory.ProductVariant.Product.Name,
            VariantName = inventory.ProductVariant.Name,
            Sku = inventory.ProductVariant.Sku,
            Size = inventory.ProductVariant.Size,
            Colour = inventory.ProductVariant.Colour,
            Price = inventory.ProductVariant.Price,
            QuantityOnHand = inventory.QuantityOnHand,
            ReservedQuantity = inventory.ReservedQuantity,
            ReorderLevel = inventory.ReorderLevel,
            IsLowStock = inventory.QuantityOnHand <= inventory.ReorderLevel,
            LastUpdatedAt = inventory.UpdatedAt
        };
    }

    public async Task<InventoryResponse> AdjustStockAsync(
        StockAdjustmentRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.QuantityChange == 0)
        {
            throw new ArgumentException("Quantity change cannot be zero.", nameof(request));
        }

        var variant = await _context.ProductVariants
            .Include(v => v.Product)
            .Include(v => v.Inventory)
            .FirstOrDefaultAsync(v => v.Id == request.ProductVariantId, cancellationToken);

        if (variant is null)
        {
            throw new InvalidOperationException($"ProductVariant with ID {request.ProductVariantId} not found.");
        }

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        var inventory = variant.Inventory;
        if (inventory is null)
        {
            inventory = new Models.Catalog.Inventory
            {
                ProductVariantId = variant.Id,
                ProductVariant = variant,
                QuantityOnHand = 0,
                ReservedQuantity = 0,
                ReorderLevel = 10
            };
            _context.Inventory.Add(inventory);
            variant.Inventory = inventory;
        }

        var newQuantity = inventory.QuantityOnHand + request.QuantityChange;

        if (newQuantity < 0)
        {
            throw new InvalidOperationException(
                $"Insufficient inventory. Current on hand is {inventory.QuantityOnHand}, but adjustment is {request.QuantityChange}.");
        }

        inventory.QuantityOnHand = newQuantity;

        var tx = new InventoryTransaction
        {
            ProductVariantId = variant.Id,
            ProductVariant = variant,
            Type = request.Type,
            QuantityChange = request.QuantityChange,
            QuantityOnHandAfter = newQuantity,
            Reference = request.Reference?.Trim(),
            Note = request.Note?.Trim()
        };

        _context.InventoryTransactions.Add(tx);

        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new InventoryResponse
        {
            Id = inventory.Id,
            ProductVariantId = variant.Id,
            ProductId = variant.ProductId,
            ProductName = variant.Product.Name,
            VariantName = variant.Name,
            Sku = variant.Sku,
            Size = variant.Size,
            Colour = variant.Colour,
            Price = variant.Price,
            QuantityOnHand = inventory.QuantityOnHand,
            ReservedQuantity = inventory.ReservedQuantity,
            ReorderLevel = inventory.ReorderLevel,
            IsLowStock = inventory.QuantityOnHand <= inventory.ReorderLevel,
            LastUpdatedAt = inventory.UpdatedAt
        };
    }

    public async Task<List<InventoryTransactionResponse>> GetTransactionsAsync(
        Guid? variantId = null,
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        var query = _context.InventoryTransactions
            .AsNoTracking()
            .Include(t => t.ProductVariant)
                .ThenInclude(v => v.Product)
            .AsQueryable();

        if (variantId.HasValue)
        {
            query = query.Where(t => t.ProductVariantId == variantId.Value);
        }

        var safeLimit = Math.Clamp(limit, 1, 200);

        return await query
            .OrderByDescending(t => t.CreatedAt)
            .Take(safeLimit)
            .Select(t => new InventoryTransactionResponse
            {
                Id = t.Id,
                ProductVariantId = t.ProductVariantId,
                Sku = t.ProductVariant.Sku,
                VariantName = t.ProductVariant.Name,
                ProductName = t.ProductVariant.Product.Name,
                Type = t.Type.ToString(),
                QuantityChange = t.QuantityChange,
                QuantityOnHandAfter = t.QuantityOnHandAfter,
                Reference = t.Reference,
                Note = t.Note,
                CreatedAt = t.CreatedAt
            })
            .ToListAsync(cancellationToken);
    }
}
