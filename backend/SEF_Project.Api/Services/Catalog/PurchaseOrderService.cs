using Microsoft.EntityFrameworkCore;
using SEF_Project.Api.Data;
using SEF_Project.Api.DTOs.Catalog;
using SEF_Project.Api.DTOs.Common;
using SEF_Project.Api.Models.Catalog;
using SEF_Project.Api.Models.Enums;

namespace SEF_Project.Api.Services.Catalog;

public class PurchaseOrderService : IPurchaseOrderService
{
    private static readonly Dictionary<PurchaseOrderStatus, PurchaseOrderStatus[]>
        AllowedTransitions = new()
        {
            [PurchaseOrderStatus.Draft] = new[]
                { PurchaseOrderStatus.Submitted, PurchaseOrderStatus.Cancelled },
            [PurchaseOrderStatus.Submitted] = new[]
                { PurchaseOrderStatus.Received, PurchaseOrderStatus.Cancelled },
            [PurchaseOrderStatus.Received] = Array.Empty<PurchaseOrderStatus>(),
            [PurchaseOrderStatus.Cancelled] = Array.Empty<PurchaseOrderStatus>()
        };

    private readonly AppDbContext _context;
    private readonly ILogger<PurchaseOrderService> _logger;

    public PurchaseOrderService(
        AppDbContext context,
        ILogger<PurchaseOrderService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<PagedResponse<PurchaseOrderResponse>> GetPurchaseOrdersAsync(
        PurchaseOrderQueryDto query,
        CancellationToken cancellationToken = default)
    {
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var purchaseOrders = _context.PurchaseOrders.AsNoTracking();

        if (query.SupplierId is not null)
        {
            purchaseOrders = purchaseOrders.Where(
                o => o.SupplierId == query.SupplierId.Value);
        }

        if (query.Status is not null)
        {
            purchaseOrders = purchaseOrders.Where(
                o => o.Status == query.Status.Value);
        }

        // Newest first, with the Id tiebreaker keeping paging stable.
        purchaseOrders = purchaseOrders
            .OrderByDescending(o => o.CreatedAt)
            .ThenByDescending(o => o.Id);

        var totalCount = await purchaseOrders.CountAsync(cancellationToken);

        var orders = await purchaseOrders
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Include(o => o.Supplier)
            .Include(o => o.Items)
                .ThenInclude(i => i.ProductVariant)
                .ThenInclude(v => v.Product)
            .ToListAsync(cancellationToken);

        return new PagedResponse<PurchaseOrderResponse>
        {
            Items = orders.Select(MapPurchaseOrder).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalItems = totalCount,
            TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
        };
    }

    public async Task<PurchaseOrderResponse?> GetPurchaseOrderByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var order = await LoadPurchaseOrderAsync(
            id,
            asNoTracking: true,
            cancellationToken);

        return order is null ? null : MapPurchaseOrder(order);
    }

    public async Task<PurchaseOrderResponse> CreatePurchaseOrderAsync(
        PurchaseOrderCreateRequest request,
        int? performedByUserId = null,
        CancellationToken cancellationToken = default)
    {
        await using var transaction =
            await _context.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            if (request.Items is null || request.Items.Count == 0)
            {
                throw new ArgumentException(
                    "A purchase order must contain at least one item.");
            }

            var supplier = await _context.Suppliers
                .FirstOrDefaultAsync(s => s.Id == request.SupplierId, cancellationToken);

            if (supplier is null)
            {
                throw new ArgumentException("Supplier was not found.");
            }

            if (!supplier.IsActive)
            {
                throw new InvalidOperationException(
                    "An inactive supplier cannot be used for a purchase order.");
            }

            var variantIds = request.Items
                .Select(i => i.ProductVariantId)
                .Distinct()
                .ToList();

            var variants = await _context.ProductVariants
                .Include(v => v.Product)
                .Where(v => variantIds.Contains(v.Id))
                .ToDictionaryAsync(v => v.Id, cancellationToken);

            foreach (var item in request.Items)
            {
                if (item.Quantity <= 0)
                {
                    throw new ArgumentException(
                        "Quantity must be greater than zero.");
                }

                if (item.UnitCost < 0)
                {
                    throw new ArgumentException(
                        "Unit cost cannot be negative.");
                }

                if (!variants.ContainsKey(item.ProductVariantId))
                {
                    throw new ArgumentException("Product variant was not found.");
                }
            }

            var now = DateTime.UtcNow;

            var order = new PurchaseOrder
            {
                OrderNumber = await GenerateOrderNumberAsync(now, cancellationToken),
                SupplierId = supplier.Id,
                Supplier = supplier,
                Status = PurchaseOrderStatus.Draft,
                ExpectedAt = request.ExpectedAt,
                Notes = NullIfWhitespace(request.Notes)
            };

            foreach (var item in request.Items)
            {
                order.Items.Add(new PurchaseOrderItem
                {
                    ProductVariantId = item.ProductVariantId,
                    ProductVariant = variants[item.ProductVariantId],
                    Quantity = item.Quantity,
                    UnitCost = Math.Round(item.UnitCost, 2)
                });
            }

            _context.PurchaseOrders.Add(order);

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            _logger.LogInformation(
                "Purchase order {OrderNumber} created for supplier {SupplierId} by user {PerformedByUserId}.",
                order.OrderNumber,
                supplier.Id,
                performedByUserId);

            return MapPurchaseOrder(order);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<PurchaseOrderResponse?> SubmitPurchaseOrderAsync(
        Guid id,
        int? performedByUserId = null,
        CancellationToken cancellationToken = default)
    {
        await using var transaction =
            await _context.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var order = await LoadPurchaseOrderAsync(
                id,
                asNoTracking: false,
                cancellationToken);

            if (order is null)
            {
                return null;
            }

            EnsureTransition(order, PurchaseOrderStatus.Submitted);

            order.Status = PurchaseOrderStatus.Submitted;
            order.SubmittedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            _logger.LogInformation(
                "Purchase order {OrderNumber} submitted by user {PerformedByUserId}.",
                order.OrderNumber,
                performedByUserId);

            return MapPurchaseOrder(order);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<PurchaseOrderResponse?> ReceivePurchaseOrderAsync(
        Guid id,
        int? performedByUserId = null,
        CancellationToken cancellationToken = default)
    {
        await using var transaction =
            await _context.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var order = await LoadPurchaseOrderAsync(
                id,
                asNoTracking: false,
                cancellationToken);

            if (order is null)
            {
                return null;
            }

            EnsureTransition(order, PurchaseOrderStatus.Received);

            if (order.Items.Count == 0)
            {
                throw new InvalidOperationException(
                    "A purchase order without items cannot be received.");
            }

            var variantIds = order.Items
                .Select(i => i.ProductVariantId)
                .Distinct()
                .ToList();

            var inventory = await _context.Inventory
                .Where(i => variantIds.Contains(i.ProductVariantId))
                .ToDictionaryAsync(i => i.ProductVariantId, cancellationToken);

            foreach (var item in order.Items)
            {
                if (!inventory.TryGetValue(item.ProductVariantId, out var stock))
                {
                    throw new InvalidOperationException(
                        "Inventory record missing for a purchase-order line.");
                }

                var quantityOnHandBefore = stock.QuantityOnHand;

                stock.QuantityOnHand = quantityOnHandBefore + item.Quantity;

                _context.InventoryTransactions.Add(new StockTransaction
                {
                    ProductVariantId = item.ProductVariantId,
                    Type = InventoryTransactionType.Receipt,
                    QuantityChange = item.Quantity,
                    QuantityOnHandBefore = quantityOnHandBefore,
                    QuantityOnHandAfter = stock.QuantityOnHand,
                    PerformedByUserId = performedByUserId,
                    Reference = order.OrderNumber,
                    Note = $"Received on purchase order {order.OrderNumber}."
                });
            }

            order.Status = PurchaseOrderStatus.Received;
            order.ReceivedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            _logger.LogInformation(
                "Purchase order {OrderNumber} received by user {PerformedByUserId}; stock increased for {LineCount} line(s).",
                order.OrderNumber,
                performedByUserId,
                order.Items.Count);

            return MapPurchaseOrder(order);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<PurchaseOrderResponse?> CancelPurchaseOrderAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        await using var transaction =
            await _context.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var order = await LoadPurchaseOrderAsync(
                id,
                asNoTracking: false,
                cancellationToken);

            if (order is null)
            {
                return null;
            }

            EnsureTransition(order, PurchaseOrderStatus.Cancelled);

            order.Status = PurchaseOrderStatus.Cancelled;

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            _logger.LogInformation(
                "Purchase order {OrderNumber} cancelled.",
                order.OrderNumber);

            return MapPurchaseOrder(order);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private async Task<PurchaseOrder?> LoadPurchaseOrderAsync(
        Guid id,
        bool asNoTracking,
        CancellationToken cancellationToken)
    {
        IQueryable<PurchaseOrder> query = _context.PurchaseOrders
            .Include(o => o.Supplier)
            .Include(o => o.Items)
                .ThenInclude(i => i.ProductVariant)
                .ThenInclude(v => v.Product);

        if (asNoTracking)
        {
            query = query.AsNoTracking();
        }

        return await query.FirstOrDefaultAsync(
            o => o.Id == id,
            cancellationToken);
    }

    /// <summary>
    /// Generates a human-readable, year-scoped and collision-safe order number
    /// (for example <c>PO-2026-0001</c>) derived the same way
    /// <c>OrderService</c> derives its numbers.
    /// </summary>
    private async Task<string> GenerateOrderNumberAsync(
        DateTime now,
        CancellationToken cancellationToken)
    {
        var prefix = $"PO-{now:yyyy}-";

        var existingNumbers = await _context.PurchaseOrders
            .Where(o => o.OrderNumber.StartsWith(prefix))
            .Select(o => o.OrderNumber)
            .ToListAsync(cancellationToken);

        var next = 0;

        foreach (var number in existingNumbers)
        {
            var suffix = number[prefix.Length..];

            if (int.TryParse(suffix, out var value) && value > next)
            {
                next = value;
            }
        }

        var candidate = $"{prefix}{next + 1:D4}";

        // Guarantee uniqueness even if a non-sequential number occupies the
        // next slot.
        while (existingNumbers.Contains(candidate))
        {
            next++;
            candidate = $"{prefix}{next + 1:D4}";
        }

        return candidate;
    }

    private static void EnsureTransition(
        PurchaseOrder order,
        PurchaseOrderStatus target)
    {
        if (order.Status == target)
        {
            throw new InvalidOperationException(
                $"Purchase order is already '{target}'.");
        }

        if (!AllowedTransitions.TryGetValue(order.Status, out var allowed)
            || !allowed.Contains(target))
        {
            throw new InvalidOperationException(
                $"Transition from '{order.Status}' to '{target}' is not allowed.");
        }
    }

    private static PurchaseOrderResponse MapPurchaseOrder(
        PurchaseOrder order) =>
        new()
        {
            Id = order.Id,
            OrderNumber = order.OrderNumber,
            SupplierId = order.SupplierId,
            SupplierName = order.Supplier?.Name ?? string.Empty,
            Status = order.Status,
            ExpectedAt = order.ExpectedAt,
            SubmittedAt = order.SubmittedAt,
            ReceivedAt = order.ReceivedAt,
            Notes = order.Notes,
            Items = order.Items
                .Select(item => new PurchaseOrderItemResponse
                {
                    Id = item.Id,
                    ProductVariantId = item.ProductVariantId,
                    Sku = item.ProductVariant?.Sku ?? string.Empty,
                    ProductName = item.ProductVariant?.Product?.Name ?? string.Empty,
                    VariantName = item.ProductVariant?.Name ?? string.Empty,
                    Quantity = item.Quantity,
                    UnitCost = item.UnitCost,
                    LineTotal = Math.Round(item.Quantity * item.UnitCost, 2)
                })
                .ToList(),
            Total = Math.Round(
                order.Items.Sum(i => i.Quantity * i.UnitCost),
                2),
            CreatedAt = order.CreatedAt,
            UpdatedAt = order.UpdatedAt
        };

    private static string? NullIfWhitespace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
