using Microsoft.EntityFrameworkCore;
using SEF_Project.Api.Data;
using SEF_Project.Api.DTOs.Orders;
using SEF_Project.Api.Models.Catalog;
using SEF_Project.Api.Models.Enums;
using SEF_Project.Api.Models.Orders;

namespace SEF_Project.Api.Services.Orders;

public class ReturnService : IReturnService
{
    private static readonly IReadOnlyDictionary<ReturnStatus, ReturnStatus[]>
        AllowedStaffTransitions = new Dictionary<ReturnStatus, ReturnStatus[]>
        {
            [ReturnStatus.Requested] =
                new[] { ReturnStatus.Approved, ReturnStatus.Rejected },
            [ReturnStatus.Approved] = new[] { ReturnStatus.Received },
            [ReturnStatus.Received] = new[] { ReturnStatus.Refunded },
            [ReturnStatus.Rejected] = Array.Empty<ReturnStatus>(),
            [ReturnStatus.Refunded] = Array.Empty<ReturnStatus>(),
            [ReturnStatus.Cancelled] = Array.Empty<ReturnStatus>()
        };

    private static readonly ReturnStatus[] QuantityHoldingStatuses =
    {
        ReturnStatus.Requested,
        ReturnStatus.Approved,
        ReturnStatus.Received,
        ReturnStatus.Refunded
    };

    private readonly AppDbContext _context;
    private readonly ILogger<ReturnService> _logger;

    public ReturnService(AppDbContext context, ILogger<ReturnService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<ReturnListResponse> GetReturnsAsync(
        int userId,
        bool canAccessAllOrders,
        ReturnQuery query,
        CancellationToken cancellationToken = default)
    {
        var returns = FullReturnQuery(asNoTracking: true);

        if (!canAccessAllOrders)
        {
            returns = returns.Where(r => r.Order.Customer.UserId == userId);
        }

        if (query.Status.HasValue)
        {
            returns = returns.Where(r => r.Status == query.Status.Value);
        }

        var orderNumber = NullIfWhitespace(query.OrderNumber);
        if (orderNumber is not null)
        {
            returns = returns.Where(r =>
                EF.Functions.Like(
                    r.Order.OrderNumber.ToLower(),
                    $"%{orderNumber.ToLower()}%"));
        }

        var totalCount = await returns.CountAsync(cancellationToken);
        var items = await returns
            .OrderByDescending(r => r.RequestedAt)
            .ThenByDescending(r => r.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        return new ReturnListResponse
        {
            Items = items.Select(BuildResponse).ToList(),
            TotalCount = totalCount,
            Page = query.Page,
            PageSize = query.PageSize
        };
    }

    public async Task<List<ReturnResponse>?> GetOrderReturnsAsync(
        int userId,
        bool canAccessAllOrders,
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        var order = await _context.Orders
            .AsNoTracking()
            .Include(o => o.Customer)
            .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);

        if (order is null || !CanView(order, userId, canAccessAllOrders))
        {
            return null;
        }

        var returns = await FullReturnQuery(asNoTracking: true)
            .Where(r => r.OrderId == orderId)
            .OrderByDescending(r => r.RequestedAt)
            .ToListAsync(cancellationToken);

        return returns.Select(BuildResponse).ToList();
    }

    public async Task<ReturnResponse?> GetReturnAsync(
        int userId,
        bool canAccessAllOrders,
        Guid returnId,
        CancellationToken cancellationToken = default)
    {
        var productReturn = await FullReturnQuery(asNoTracking: true)
            .FirstOrDefaultAsync(r => r.Id == returnId, cancellationToken);

        return productReturn is null
            || !CanView(productReturn.Order, userId, canAccessAllOrders)
            ? null
            : BuildResponse(productReturn);
    }

    public async Task<ReturnResponse?> CreateReturnAsync(
        int userId,
        bool canAccessAllOrders,
        Guid orderId,
        CreateReturnRequest request,
        CancellationToken cancellationToken = default)
    {
        await using var transaction =
            await _context.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var order = await _context.Orders
                .Include(o => o.Customer)
                .Include(o => o.Items)
                    .ThenInclude(i => i.ProductVariant)
                    .ThenInclude(v => v.Product)
                .Include(o => o.Returns)
                    .ThenInclude(r => r.Items)
                .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);

            if (order is null || !CanView(order, userId, canAccessAllOrders))
            {
                return null;
            }

            if (order.Status != OrderStatus.Completed)
            {
                throw new InvalidOperationException(
                    "Returns can be requested only after an order is completed.");
            }

            var requestedItems = request.Items
                .GroupBy(i => i.OrderItemId)
                .Select(group => new
                {
                    OrderItemId = group.Key,
                    Quantity = group.Sum(item => item.Quantity)
                })
                .ToList();

            if (requestedItems.Count == 0
                || requestedItems.Any(item =>
                    item.OrderItemId == Guid.Empty || item.Quantity <= 0))
            {
                throw new ArgumentException(
                    "At least one valid return item is required.");
            }

            var orderItems = order.Items.ToDictionary(item => item.Id);
            var heldQuantities = order.Returns
                .Where(r => QuantityHoldingStatuses.Contains(r.Status))
                .SelectMany(r => r.Items)
                .GroupBy(item => item.OrderItemId)
                .ToDictionary(group => group.Key, group => group.Sum(i => i.Quantity));

            foreach (var item in requestedItems)
            {
                if (!orderItems.TryGetValue(item.OrderItemId, out var orderItem))
                {
                    throw new ArgumentException(
                        "A selected item does not belong to this order.");
                }

                var alreadyHeld = heldQuantities.GetValueOrDefault(item.OrderItemId);
                if (alreadyHeld + item.Quantity > orderItem.Quantity)
                {
                    throw new InvalidOperationException(
                        $"Return quantity for '{orderItem.ProductVariant.Product.Name}' exceeds the remaining returnable quantity.");
                }
            }

            var now = DateTime.UtcNow;
            var productReturn = new ProductReturn
            {
                ReturnNumber = GenerateReturnNumber(now),
                OrderId = order.Id,
                Status = ReturnStatus.Requested,
                Reason = request.Reason,
                CustomerNote = NullIfWhitespace(request.Note),
                RequestedAt = now
            };

            foreach (var item in requestedItems)
            {
                var orderItem = orderItems[item.OrderItemId];
                productReturn.Items.Add(new ReturnItem
                {
                    OrderItemId = orderItem.Id,
                    Quantity = item.Quantity,
                    UnitRefundAmount = orderItem.UnitPrice
                });
            }

            productReturn.RefundAmount = Math.Round(
                productReturn.Items.Sum(i => i.UnitRefundAmount * i.Quantity),
                2);

            _context.Returns.Add(productReturn);
            await _context.SaveChangesAsync(cancellationToken);
            var response = await GetReturnAsync(
                userId,
                canAccessAllOrders,
                productReturn.Id,
                cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            _logger.LogInformation(
                "Return {ReturnNumber} requested for order {OrderNumber}.",
                productReturn.ReturnNumber,
                order.OrderNumber);

            return response;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<ReturnResponse?> UpdateStatusAsync(
        int userId,
        bool canManageReturns,
        Guid returnId,
        UpdateReturnStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!canManageReturns)
        {
            throw new UnauthorizedAccessException(
                "Only staff can review and process returns.");
        }

        await using var transaction =
            await _context.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var productReturn = await FullReturnQuery(asNoTracking: false)
                .FirstOrDefaultAsync(r => r.Id == returnId, cancellationToken);

            if (productReturn is null)
            {
                return null;
            }

            if (!AllowedStaffTransitions.TryGetValue(
                    productReturn.Status,
                    out var allowed)
                || !allowed.Contains(request.Status))
            {
                throw new InvalidOperationException(
                    $"Return cannot move from '{productReturn.Status}' to '{request.Status}'.");
            }

            var now = DateTime.UtcNow;
            productReturn.Status = request.Status;
            productReturn.StaffNote =
                NullIfWhitespace(request.Note) ?? productReturn.StaffNote;
            productReturn.ReviewedByUserId = userId;

            if (request.Status is ReturnStatus.Approved or ReturnStatus.Rejected)
            {
                productReturn.ReviewedAt = now;
            }
            else if (request.Status == ReturnStatus.Received)
            {
                productReturn.ReceivedAt = now;
                await RestockReturnAsync(productReturn, userId, cancellationToken);
            }
            else if (request.Status == ReturnStatus.Refunded)
            {
                await RecordRefundAsync(productReturn, now, cancellationToken);
                productReturn.RefundedAt = now;
                await MarkOrderRefundedWhenFullyReturnedAsync(
                    productReturn.Order,
                    userId,
                    now,
                    cancellationToken);
            }

            await _context.SaveChangesAsync(cancellationToken);
            var response = await GetReturnAsync(
                userId,
                canAccessAllOrders: true,
                returnId,
                cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return response;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<ReturnResponse?> CancelReturnAsync(
        int userId,
        bool canAccessAllOrders,
        Guid returnId,
        CancelReturnRequest request,
        CancellationToken cancellationToken = default)
    {
        var productReturn = await FullReturnQuery(asNoTracking: false)
            .FirstOrDefaultAsync(r => r.Id == returnId, cancellationToken);

        if (productReturn is null
            || !CanView(productReturn.Order, userId, canAccessAllOrders))
        {
            return null;
        }

        if (productReturn.Status != ReturnStatus.Requested)
        {
            throw new InvalidOperationException(
                "Only a pending return request can be cancelled.");
        }

        productReturn.Status = ReturnStatus.Cancelled;
        productReturn.CustomerNote =
            NullIfWhitespace(request.Note) ?? productReturn.CustomerNote;

        await _context.SaveChangesAsync(cancellationToken);
        return BuildResponse(productReturn);
    }

    private async Task RestockReturnAsync(
        ProductReturn productReturn,
        int userId,
        CancellationToken cancellationToken)
    {
        var variantIds = productReturn.Items
            .Select(i => i.OrderItem.ProductVariantId)
            .Distinct()
            .ToList();
        var inventory = await _context.Inventory
            .Where(stock => variantIds.Contains(stock.ProductVariantId))
            .ToDictionaryAsync(stock => stock.ProductVariantId, cancellationToken);

        foreach (var group in productReturn.Items.GroupBy(
                     item => item.OrderItem.ProductVariantId))
        {
            if (!inventory.TryGetValue(group.Key, out var stock))
            {
                throw new InvalidOperationException(
                    "Inventory record missing for a returned variant.");
            }

            var quantity = group.Sum(item => item.Quantity);
            var quantityBefore = stock.QuantityOnHand;
            stock.QuantityOnHand += quantity;

            _context.InventoryTransactions.Add(new StockTransaction
            {
                ProductVariantId = group.Key,
                Type = InventoryTransactionType.Return,
                QuantityChange = quantity,
                QuantityOnHandBefore = quantityBefore,
                QuantityOnHandAfter = stock.QuantityOnHand,
                PerformedByUserId = userId,
                Reference = productReturn.ReturnNumber,
                Note = "Customer return received."
            });
        }
    }

    private async Task RecordRefundAsync(
        ProductReturn productReturn,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var completedPayments = productReturn.Order.Payments
            .Where(payment => payment.Status == PaymentStatus.Completed)
            .ToList();
        var paidAmount = completedPayments.Sum(payment => payment.Amount);
        var refundedAmount = productReturn.Order.Payments
            .Where(payment =>
                payment.Status == PaymentStatus.Refunded
                && payment.TransactionReference != null
                && payment.TransactionReference.StartsWith("RETURN-"))
            .Sum(payment => payment.Amount);

        if (paidAmount - refundedAmount < productReturn.RefundAmount)
        {
            throw new InvalidOperationException(
                "The return refund exceeds the completed payment balance.");
        }

        var refund = new Payment
        {
            OrderId = productReturn.OrderId,
            Amount = productReturn.RefundAmount,
            Method = completedPayments.FirstOrDefault()?.Method
                ?? PaymentMethod.Cash,
            Status = PaymentStatus.Refunded,
            TransactionReference = $"RETURN-{productReturn.ReturnNumber}",
            PaidAt = now
        };

        _context.Payments.Add(refund);
        await Task.CompletedTask;
    }

    private async Task MarkOrderRefundedWhenFullyReturnedAsync(
        Order order,
        int userId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var returnedByItem = order.Returns
            .Where(r => r.Status == ReturnStatus.Refunded)
            .SelectMany(r => r.Items)
            .GroupBy(item => item.OrderItemId)
            .ToDictionary(group => group.Key, group => group.Sum(i => i.Quantity));

        var fullyReturned = order.Items.All(item =>
            returnedByItem.GetValueOrDefault(item.Id) >= item.Quantity);

        if (!fullyReturned || order.Status == OrderStatus.Refunded)
        {
            return;
        }

        order.Status = OrderStatus.Refunded;
        _context.OrderStatusHistory.Add(new OrderStatusHistory
        {
            OrderId = order.Id,
            Status = OrderStatus.Refunded,
            ChangedAt = now,
            ChangedByUserId = userId,
            Note = "All order items were returned and refunded."
        });
        await Task.CompletedTask;
    }

    private IQueryable<ProductReturn> FullReturnQuery(bool asNoTracking)
    {
        IQueryable<ProductReturn> query = _context.Returns
            .Include(r => r.Order)
                .ThenInclude(o => o.Customer)
            .Include(r => r.Order)
                .ThenInclude(o => o.Items)
            .Include(r => r.Order)
                .ThenInclude(o => o.Payments)
            .Include(r => r.Items)
                .ThenInclude(item => item.OrderItem)
                .ThenInclude(orderItem => orderItem.ProductVariant)
                .ThenInclude(variant => variant.Product);

        if (!asNoTracking)
        {
            query = query
                .Include(r => r.Order)
                    .ThenInclude(o => o.Returns)
                    .ThenInclude(otherReturn => otherReturn.Items);
        }

        return asNoTracking ? query.AsNoTracking() : query;
    }

    private static bool CanView(
        Order order,
        int userId,
        bool canAccessAllOrders) =>
        canAccessAllOrders || order.Customer.UserId == userId;

    private static ReturnResponse BuildResponse(ProductReturn productReturn) =>
        new()
        {
            Id = productReturn.Id,
            ReturnNumber = productReturn.ReturnNumber,
            OrderId = productReturn.OrderId,
            OrderNumber = productReturn.Order.OrderNumber,
            Status = productReturn.Status,
            Reason = productReturn.Reason,
            CustomerNote = productReturn.CustomerNote,
            StaffNote = productReturn.StaffNote,
            RefundAmount = productReturn.RefundAmount,
            Currency = productReturn.Order.Currency,
            RequestedAt = productReturn.RequestedAt,
            ReviewedAt = productReturn.ReviewedAt,
            ReceivedAt = productReturn.ReceivedAt,
            RefundedAt = productReturn.RefundedAt,
            Items = productReturn.Items
                .OrderBy(item => item.CreatedAt)
                .Select(item => new ReturnItemResponse
                {
                    Id = item.Id,
                    OrderItemId = item.OrderItemId,
                    ProductVariantId = item.OrderItem.ProductVariantId,
                    Name = item.OrderItem.ProductVariant.Product.Name,
                    Sku = item.OrderItem.ProductVariant.Sku,
                    Quantity = item.Quantity,
                    UnitRefundAmount = item.UnitRefundAmount,
                    LineRefundAmount = Math.Round(
                        item.UnitRefundAmount * item.Quantity,
                        2)
                })
                .ToList()
        };

    private static string GenerateReturnNumber(DateTime now) =>
        $"RET-{now:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";

    private static string? NullIfWhitespace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
