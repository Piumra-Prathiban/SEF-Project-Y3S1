using Microsoft.EntityFrameworkCore;
using SEF_Project.Api.Data;
using SEF_Project.Api.DTOs.Orders;
using SEF_Project.Api.Models;
using SEF_Project.Api.Models.Catalog;
using SEF_Project.Api.Models.Enums;
using SEF_Project.Api.Models.Orders;

namespace SEF_Project.Api.Services.Orders;

public class OrderService : IOrderService
{
    private static readonly Dictionary<OrderStatus, OrderStatus[]>
        AllowedTransitions = new()
        {
            [OrderStatus.Pending] = new[]
                { OrderStatus.Confirmed, OrderStatus.Cancelled },
            [OrderStatus.Confirmed] = new[]
                { OrderStatus.Preparing, OrderStatus.Cancelled },
            [OrderStatus.Preparing] = new[]
                { OrderStatus.Ready, OrderStatus.Cancelled },
            [OrderStatus.Ready] = new[]
                { OrderStatus.Completed, OrderStatus.Cancelled },
            [OrderStatus.Completed] = new[] { OrderStatus.Refunded },
            [OrderStatus.Cancelled] = Array.Empty<OrderStatus>(),
            [OrderStatus.Refunded] = Array.Empty<OrderStatus>()
        };

    private static readonly Dictionary<PaymentStatus, PaymentStatus[]>
        AllowedPaymentTransitions = new()
        {
            [PaymentStatus.Pending] = new[]
                { PaymentStatus.Completed, PaymentStatus.Failed },
            [PaymentStatus.Completed] = new[] { PaymentStatus.Refunded },
            [PaymentStatus.Failed] = Array.Empty<PaymentStatus>(),
            [PaymentStatus.Refunded] = Array.Empty<PaymentStatus>()
        };

    private static readonly Dictionary<ShipmentStatus, ShipmentStatus[]>
        AllowedShipmentTransitions = new()
        {
            [ShipmentStatus.Pending] = new[]
                { ShipmentStatus.Shipped, ShipmentStatus.Cancelled },
            [ShipmentStatus.Shipped] = new[] { ShipmentStatus.Delivered },
            [ShipmentStatus.Delivered] = Array.Empty<ShipmentStatus>(),
            [ShipmentStatus.Cancelled] = Array.Empty<ShipmentStatus>()
        };

    private readonly AppDbContext _context;
    private readonly ILogger<OrderService> _logger;

    public OrderService(
        AppDbContext context,
        ILogger<OrderService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<OrderResponse> CreateOrderAsync(
        int userId,
        CreateOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        await using var transaction =
            await _context.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var customer = await ResolveCustomerAsync(userId, cancellationToken);

            var items = MergeItems(request.Items);

            foreach (var item in items)
            {
                if (item.Quantity <= 0)
                {
                    throw new ArgumentException(
                        "Quantity must be greater than zero.");
                }
            }

            var variantIds = items
                .Select(i => i.ProductVariantId)
                .Distinct()
                .ToList();

            var variants = await _context.ProductVariants
                .Include(v => v.Product)
                .Where(v => variantIds.Contains(v.Id))
                .ToDictionaryAsync(v => v.Id, cancellationToken);

            var stock = await _context.Inventory
                .Where(i => variantIds.Contains(i.ProductVariantId))
                .ToDictionaryAsync(i => i.ProductVariantId, cancellationToken);

            ValidateAvailability(items, variants, stock);

            var now = DateTime.UtcNow;

            var order = new Order
            {
                OrderNumber = GenerateOrderNumber(now),
                CustomerId = customer.Id,
                Status = OrderStatus.Pending,
                Currency = "LKR",
                PlacedAt = now
            };

            var subtotal = 0m;

            foreach (var item in items)
            {
                var variant = variants[item.ProductVariantId];
                var lineTotal = Math.Round(variant.Price * item.Quantity, 2);

                order.Items.Add(new OrderItem
                {
                    ProductVariant = variant,
                    Quantity = item.Quantity,
                    UnitPrice = variant.Price,
                    LineTotal = lineTotal
                });

                subtotal = Math.Round(subtotal + lineTotal, 2);
            }

            order.Subtotal = subtotal;
            order.Total = subtotal;

            order.DeliveryAddress = BuildAddress(request.DeliveryAddress);
            order.StatusHistory.Add(new OrderStatusHistory
            {
                Status = OrderStatus.Pending,
                ChangedAt = now,
                Note = "Order created."
            });

            _context.Orders.Add(order);

            foreach (var item in items)
            {
                var inventory = stock[item.ProductVariantId];
                inventory.ReservedQuantity += item.Quantity;

                _context.InventoryTransactions.Add(new InventoryTransaction
                {
                    ProductVariantId = item.ProductVariantId,
                    Type = InventoryTransactionType.Reservation,
                    // QuantityChange reflects the change in available stock;
                    // QuantityOnHandAfter snapshots the physical on-hand quantity.
                    QuantityChange = -item.Quantity,
                    QuantityOnHandAfter = inventory.QuantityOnHand,
                    Reference = order.OrderNumber
                });
            }

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            _logger.LogInformation(
                "Order {OrderNumber} created for customer {CustomerId}.",
                order.OrderNumber,
                customer.Id);

            return BuildResponse(order);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<OrderListResponse> GetOrdersAsync(
        int userId,
        bool canAccessAllOrders,
        OrderQuery query,
        CancellationToken cancellationToken = default)
    {
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var orders = _context.Orders.AsNoTracking();

        if (!canAccessAllOrders)
        {
            orders = orders.Where(o => o.Customer.UserId == userId);
        }
        else if (query.CustomerId is not null)
        {
            orders = orders.Where(o => o.CustomerId == query.CustomerId.Value);
        }

        if (query.Status is not null)
        {
            orders = orders.Where(o => o.Status == query.Status.Value);
        }

        if (query.From is not null)
        {
            orders = orders.Where(o => o.PlacedAt >= query.From.Value);
        }

        if (query.To is not null)
        {
            orders = orders.Where(o => o.PlacedAt <= query.To.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.OrderNumber))
        {
            var search = query.OrderNumber.Trim();
            orders = orders.Where(o => o.OrderNumber.Contains(search));
        }

        orders = ApplySorting(orders, query);

        var totalCount = await orders.CountAsync(cancellationToken);

        var items = await orders
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(o => new OrderSummaryResponse
            {
                Id = o.Id,
                OrderNumber = o.OrderNumber,
                Status = o.Status,
                PlacedAt = o.PlacedAt,
                Total = o.Total,
                Currency = o.Currency
            })
            .ToListAsync(cancellationToken);

        return new OrderListResponse
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<OrderResponse?> GetOrderByIdAsync(
        int userId,
        bool canAccessAllOrders,
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        var order = await LoadFullOrderAsync(
            orderId,
            asNoTracking: true,
            cancellationToken);

        if (order is null || !CanView(order, userId, canAccessAllOrders))
        {
            return null;
        }

        return BuildResponse(order);
    }

    public async Task<List<OrderStatusHistoryResponse>?> GetOrderStatusHistoryAsync(
        int userId,
        bool canAccessAllOrders,
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        var order = await _context.Orders
            .AsNoTracking()
            .Include(o => o.Customer)
            .Include(o => o.StatusHistory)
            .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);

        if (order is null || !CanView(order, userId, canAccessAllOrders))
        {
            return null;
        }

        return order.StatusHistory
            .OrderBy(h => h.ChangedAt)
            .Select(h => new OrderStatusHistoryResponse
            {
                Id = h.Id,
                Status = h.Status,
                ChangedAt = h.ChangedAt,
                Note = h.Note
            })
            .ToList();
    }

    public async Task<OrderResponse?> UpdateOrderStatusAsync(
        int userId,
        bool canManageOrders,
        Guid orderId,
        UpdateOrderStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!canManageOrders)
        {
            throw new UnauthorizedAccessException(
                "Only staff can change order status.");
        }

        await using var transaction =
            await _context.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var order = await LoadFullOrderAsync(
                orderId,
                asNoTracking: false,
                cancellationToken);

            if (order is null)
            {
                return null;
            }

            if (!IsTransitionAllowed(order.Status, request.Status))
            {
                throw new InvalidOperationException(
                    $"Transition from '{order.Status}' to '{request.Status}' is not allowed.");
            }

            order.Status = request.Status;
            order.StatusHistory.Add(new OrderStatusHistory
            {
                Status = request.Status,
                ChangedAt = DateTime.UtcNow,
                ChangedByUserId = userId,
                Note = NullIfWhitespace(request.Note)
            });

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return BuildResponse(order);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<PaymentResponse?> CreatePaymentAsync(
        int userId,
        bool canAccessAllOrders,
        Guid orderId,
        CreatePaymentRequest request,
        CancellationToken cancellationToken = default)
    {
        var order = await _context.Orders
            .Include(o => o.Customer)
            .Include(o => o.Payments)
            .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);

        if (order is null || !CanView(order, userId, canAccessAllOrders))
        {
            return null;
        }

        var amount = Math.Round(request.Amount, 2);

        if (amount <= 0)
        {
            throw new ArgumentException(
                "Payment amount must be greater than zero.");
        }

        if (order.Status == OrderStatus.Cancelled
            || order.Status == OrderStatus.Refunded)
        {
            throw new InvalidOperationException(
                "Payments are not allowed for this order.");
        }

        var outstanding = CalculateOutstandingBalance(order);

        if (outstanding <= 0)
        {
            throw new InvalidOperationException(
                "Order is already fully paid.");
        }

        if (amount > outstanding)
        {
            throw new InvalidOperationException(
                "Payment amount exceeds the outstanding balance.");
        }

        var payment = new Payment
        {
            OrderId = order.Id,
            Method = request.Method,
            Amount = amount,
            Status = PaymentStatus.Pending
        };

        _context.Payments.Add(payment);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Payment recorded for order {OrderNumber} (amount {Amount}).",
            order.OrderNumber,
            amount);

        return BuildPaymentResponse(payment);
    }

    public async Task<List<PaymentResponse>?> GetPaymentsAsync(
        int userId,
        bool canAccessAllOrders,
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        var order = await _context.Orders
            .AsNoTracking()
            .Include(o => o.Customer)
            .Include(o => o.Payments)
            .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);

        if (order is null || !CanView(order, userId, canAccessAllOrders))
        {
            return null;
        }

        return order.Payments
            .OrderByDescending(p => p.CreatedAt)
            .Select(BuildPaymentResponse)
            .ToList();
    }

    public async Task<PaymentResponse?> UpdatePaymentStatusAsync(
        int userId,
        bool canManageOrders,
        Guid orderId,
        Guid paymentId,
        UpdatePaymentStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!canManageOrders)
        {
            throw new UnauthorizedAccessException(
                "Only staff can update payments.");
        }

        await using var transaction =
            await _context.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var order = await _context.Orders
                .Include(o => o.Payments)
                .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);

            if (order is null)
            {
                return null;
            }

            var payment = order.Payments
                .FirstOrDefault(p => p.Id == paymentId);

            if (payment is null)
            {
                return null;
            }

            if (!IsPaymentTransitionAllowed(payment.Status, request.Status))
            {
                throw new InvalidOperationException(
                    $"Transition from '{payment.Status}' to '{request.Status}' is not allowed.");
            }

            payment.Status = request.Status;

            if (request.Status == PaymentStatus.Completed)
            {
                payment.PaidAt = DateTime.UtcNow;
                payment.TransactionReference =
                    NullIfWhitespace(request.TransactionReference)
                    ?? $"MOCK-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";
            }

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return BuildPaymentResponse(payment);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<ShipmentResponse?> CreateShipmentAsync(
        int userId,
        bool canManageOrders,
        Guid orderId,
        CreateShipmentRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!canManageOrders)
        {
            throw new UnauthorizedAccessException(
                "Only staff can manage shipments.");
        }

        var order = await _context.Orders
            .Include(o => o.Shipments)
            .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);

        if (order is null)
        {
            return null;
        }

        if (order.Status == OrderStatus.Cancelled
            || order.Status == OrderStatus.Refunded
            || order.Status == OrderStatus.Completed)
        {
            throw new InvalidOperationException(
                "A shipment cannot be created for this order.");
        }

        if (order.Shipments.Count > 0)
        {
            throw new InvalidOperationException(
                "A shipment already exists for this order.");
        }

        var shipment = new Shipment
        {
            OrderId = order.Id,
            Status = ShipmentStatus.Pending,
            Carrier = NullIfWhitespace(request.Carrier),
            TrackingNumber = NullIfWhitespace(request.TrackingNumber)
        };

        _context.Shipments.Add(shipment);
        await _context.SaveChangesAsync(cancellationToken);

        return BuildShipmentResponse(shipment);
    }

    public async Task<List<ShipmentResponse>?> GetShipmentsAsync(
        int userId,
        bool canAccessAllOrders,
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        var order = await _context.Orders
            .AsNoTracking()
            .Include(o => o.Customer)
            .Include(o => o.Shipments)
            .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);

        if (order is null || !CanView(order, userId, canAccessAllOrders))
        {
            return null;
        }

        return order.Shipments
            .OrderByDescending(s => s.CreatedAt)
            .Select(BuildShipmentResponse)
            .ToList();
    }

    public async Task<ShipmentResponse?> UpdateShipmentStatusAsync(
        int userId,
        bool canManageOrders,
        Guid orderId,
        Guid shipmentId,
        UpdateShipmentRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!canManageOrders)
        {
            throw new UnauthorizedAccessException(
                "Only staff can manage shipments.");
        }

        await using var transaction =
            await _context.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var order = await _context.Orders
                .Include(o => o.Shipments)
                .Include(o => o.StatusHistory)
                .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);

            if (order is null)
            {
                return null;
            }

            var shipment = order.Shipments
                .FirstOrDefault(s => s.Id == shipmentId);

            if (shipment is null)
            {
                return null;
            }

            if (!IsShipmentTransitionAllowed(shipment.Status, request.Status))
            {
                throw new InvalidOperationException(
                    $"Transition from '{shipment.Status}' to '{request.Status}' is not allowed.");
            }

            if (request.Status == ShipmentStatus.Shipped
                && order.Status != OrderStatus.Ready)
            {
                throw new InvalidOperationException(
                    "Order must be ready before it can be shipped.");
            }

            if (request.Status == ShipmentStatus.Delivered
                && order.Status != OrderStatus.Ready
                && order.Status != OrderStatus.Completed)
            {
                throw new InvalidOperationException(
                    "Order must be ready before it can be delivered.");
            }

            var now = DateTime.UtcNow;

            shipment.Status = request.Status;
            shipment.Carrier =
                NullIfWhitespace(request.Carrier) ?? shipment.Carrier;
            shipment.TrackingNumber =
                NullIfWhitespace(request.TrackingNumber)
                ?? shipment.TrackingNumber;

            if (request.Status == ShipmentStatus.Shipped)
            {
                shipment.ShippedAt = now;
            }
            else if (request.Status == ShipmentStatus.Delivered)
            {
                shipment.DeliveredAt = now;

                if (order.Status == OrderStatus.Ready)
                {
                    order.Status = OrderStatus.Completed;

                    var history = new OrderStatusHistory
                    {
                        Status = OrderStatus.Completed,
                        ChangedAt = now,
                        ChangedByUserId = userId,
                        Note = "Marked delivered."
                    };

                    order.StatusHistory.Add(history);
                    _context.OrderStatusHistory.Add(history);
                }
            }

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return BuildShipmentResponse(shipment);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<OrderResponse?> CancelOrderAsync(
        int userId,
        bool canAccessAllOrders,
        Guid orderId,
        CancelOrderRequest request,
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
                .Include(o => o.Payments)
                .Include(o => o.Shipments)
                .Include(o => o.StatusHistory)
                .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);

            if (order is null || !CanView(order, userId, canAccessAllOrders))
            {
                return null;
            }

            if (!IsTransitionAllowed(order.Status, OrderStatus.Cancelled))
            {
                throw new InvalidOperationException(
                    $"Order cannot be cancelled from '{order.Status}'.");
            }

            var shippedShipment = order.Shipments.FirstOrDefault(
                s => s.Status == ShipmentStatus.Shipped);

            if (shippedShipment is not null)
            {
                throw new InvalidOperationException(
                    "Order cannot be cancelled because it has already been shipped.");
            }

            var now = DateTime.UtcNow;

            order.Status = OrderStatus.Cancelled;

            var history = new OrderStatusHistory
            {
                Status = OrderStatus.Cancelled,
                ChangedAt = now,
                ChangedByUserId = userId,
                Note = NullIfWhitespace(request.Reason) ?? "Order cancelled."
            };

            order.StatusHistory.Add(history);
            _context.OrderStatusHistory.Add(history);

            foreach (var shipment in order.Shipments
                         .Where(s => s.Status == ShipmentStatus.Pending))
            {
                shipment.Status = ShipmentStatus.Cancelled;
            }

            foreach (var payment in order.Payments)
            {
                if (payment.Status == PaymentStatus.Completed)
                {
                    payment.Status = PaymentStatus.Refunded;
                }
                else if (payment.Status == PaymentStatus.Pending)
                {
                    payment.Status = PaymentStatus.Failed;
                }
            }

            await ReleaseReservedInventoryAsync(order, cancellationToken);

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            _logger.LogInformation(
                "Order {OrderNumber} cancelled.",
                order.OrderNumber);

            return BuildResponse(order);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private async Task<Customer> ResolveCustomerAsync(
        int userId,
        CancellationToken cancellationToken)
    {
        var customer = await _context.Customers
            .Include(c => c.User)
            .FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken);

        if (customer is null)
        {
            throw new UnauthorizedAccessException(
                "Customer profile not found.");
        }

        if (!customer.User.IsActive)
        {
            throw new UnauthorizedAccessException(
                "This account is inactive.");
        }

        return customer;
    }

    private async Task ReleaseReservedInventoryAsync(
        Order order,
        CancellationToken cancellationToken)
    {
        if (order.Items.Count == 0)
        {
            return;
        }

        var variantIds = order.Items
            .Select(i => i.ProductVariantId)
            .Distinct()
            .ToList();

        var inventory = await _context.Inventory
            .Where(i => variantIds.Contains(i.ProductVariantId))
            .ToDictionaryAsync(i => i.ProductVariantId, cancellationToken);

        foreach (var group in order.Items.GroupBy(i => i.ProductVariantId))
        {
            var quantity = group.Sum(i => i.Quantity);

            if (!inventory.TryGetValue(group.Key, out var stock))
            {
                throw new InvalidOperationException(
                    "Inventory record missing for a reserved variant.");
            }

            if (stock.ReservedQuantity < quantity)
            {
                throw new InvalidOperationException(
                    "Reserved inventory is inconsistent for this order.");
            }

            stock.ReservedQuantity -= quantity;

            _context.InventoryTransactions.Add(new InventoryTransaction
            {
                ProductVariantId = group.Key,
                Type = InventoryTransactionType.ReservationRelease,
                // QuantityChange reflects the change in available stock;
                // QuantityOnHandAfter snapshots the physical on-hand quantity.
                QuantityChange = quantity,
                QuantityOnHandAfter = stock.QuantityOnHand,
                Reference = order.OrderNumber
            });
        }
    }

    private async Task<Order?> LoadFullOrderAsync(
        Guid orderId,
        bool asNoTracking,
        CancellationToken cancellationToken)
    {
        IQueryable<Order> query = _context.Orders
            .Include(o => o.Customer)
            .Include(o => o.Items)
                .ThenInclude(i => i.ProductVariant)
                .ThenInclude(v => v.Product)
            .Include(o => o.DeliveryAddress)
            .Include(o => o.Payments)
            .Include(o => o.Shipments)
            .Include(o => o.StatusHistory);

        if (asNoTracking)
        {
            query = query.AsNoTracking();
        }

        return await query.FirstOrDefaultAsync(
            o => o.Id == orderId,
            cancellationToken);
    }

    private static bool CanView(
        Order order,
        int userId,
        bool canAccessAllOrders) =>
        canAccessAllOrders || order.Customer.UserId == userId;

    private static bool IsTransitionAllowed(
        OrderStatus current,
        OrderStatus target)
    {
        if (current == target)
        {
            return false;
        }

        return AllowedTransitions.TryGetValue(current, out var targets)
            && targets.Contains(target);
    }

    private static bool IsPaymentTransitionAllowed(
        PaymentStatus current,
        PaymentStatus target)
    {
        if (current == target)
        {
            return false;
        }

        return AllowedPaymentTransitions.TryGetValue(current, out var targets)
            && targets.Contains(target);
    }

    private static bool IsShipmentTransitionAllowed(
        ShipmentStatus current,
        ShipmentStatus target)
    {
        if (current == target)
        {
            return false;
        }

        return AllowedShipmentTransitions.TryGetValue(current, out var targets)
            && targets.Contains(target);
    }

    private static decimal CalculateOutstandingBalance(Order order)
    {
        var completed = order.Payments
            .Where(p => p.Status == PaymentStatus.Completed)
            .Select(p => p.Amount)
            .DefaultIfEmpty(0m)
            .Sum();

        var pending = order.Payments
            .Where(p => p.Status == PaymentStatus.Pending)
            .Select(p => p.Amount)
            .DefaultIfEmpty(0m)
            .Sum();

        return order.Total - completed - pending;
    }

    private static PaymentResponse BuildPaymentResponse(Payment payment) =>
        new()
        {
            Id = payment.Id,
            Amount = payment.Amount,
            Method = payment.Method,
            Status = payment.Status,
            TransactionReference = payment.TransactionReference,
            PaidAt = payment.PaidAt
        };

    private static ShipmentResponse BuildShipmentResponse(
        Shipment shipment) =>
        new()
        {
            Id = shipment.Id,
            Status = shipment.Status,
            TrackingNumber = shipment.TrackingNumber,
            Carrier = shipment.Carrier,
            ShippedAt = shipment.ShippedAt,
            DeliveredAt = shipment.DeliveredAt
        };

    private static void ValidateAvailability(
        List<CreateOrderItemRequest> items,
        Dictionary<Guid, ProductVariant> variants,
        Dictionary<Guid, Inventory> stock)
    {
        foreach (var item in items)
        {
            if (!variants.TryGetValue(item.ProductVariantId, out var variant))
            {
                throw new ArgumentException("Invalid product variant.");
            }

            if (!variant.IsActive)
            {
                throw new ArgumentException(
                    $"Product variant '{variant.Sku}' is not available.");
            }

            if (!stock.TryGetValue(item.ProductVariantId, out var inventory)
                || inventory.QuantityOnHand - inventory.ReservedQuantity
                    < item.Quantity)
            {
                throw new InvalidOperationException(
                    $"Insufficient stock for '{variant.Sku}'.");
            }
        }
    }

    private static IQueryable<Order> ApplySorting(
        IQueryable<Order> orders,
        OrderQuery query)
    {
        var descending = !string.Equals(
            query.SortDirection,
            "asc",
            StringComparison.OrdinalIgnoreCase);

        return query.SortBy?.ToLowerInvariant() switch
        {
            "total" => descending
                ? orders.OrderByDescending(o => (double)o.Total)
                : orders.OrderBy(o => (double)o.Total),
            "ordernumber" => descending
                ? orders.OrderByDescending(o => o.OrderNumber)
                : orders.OrderBy(o => o.OrderNumber),
            "status" => descending
                ? orders.OrderByDescending(o => o.Status)
                : orders.OrderBy(o => o.Status),
            "createdat" => descending
                ? orders.OrderByDescending(o => o.CreatedAt)
                : orders.OrderBy(o => o.CreatedAt),
            _ => descending
                ? orders.OrderByDescending(o => o.PlacedAt)
                : orders.OrderBy(o => o.PlacedAt)
        };
    }

    private static List<CreateOrderItemRequest> MergeItems(
        IEnumerable<CreateOrderItemRequest> requestItems)
    {
        return requestItems
            .GroupBy(i => i.ProductVariantId)
            .Select(group => new CreateOrderItemRequest
            {
                ProductVariantId = group.Key,
                Quantity = group.Sum(i => i.Quantity)
            })
            .ToList();
    }

    private static OrderAddress BuildAddress(
        CreateOrderAddressRequest request)
    {
        return new OrderAddress
        {
            FullName = request.FullName.Trim(),
            Line1 = request.Line1.Trim(),
            Line2 = NullIfWhitespace(request.Line2),
            City = request.City.Trim(),
            Province = NullIfWhitespace(request.Province),
            PostalCode = request.PostalCode.Trim(),
            Country = string.IsNullOrWhiteSpace(request.Country)
                ? "Sri Lanka"
                : request.Country.Trim(),
            Phone = NullIfWhitespace(request.Phone)
        };
    }

    private static string GenerateOrderNumber(DateTime now)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        return $"ORD-{now:yyyyMMdd}-{suffix}";
    }

    private static string? NullIfWhitespace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static OrderResponse BuildResponse(Order order)
    {
        return new OrderResponse
        {
            Id = order.Id,
            OrderNumber = order.OrderNumber,
            Status = order.Status,
            PlacedAt = order.PlacedAt,
            Subtotal = order.Subtotal,
            DiscountTotal = order.DiscountTotal,
            TaxAmount = order.TaxAmount,
            ShippingFee = order.ShippingFee,
            Total = order.Total,
            Currency = order.Currency,
            Items = order.Items.Select(item => new OrderItemResponse
            {
                Id = item.Id,
                ProductVariantId = item.ProductVariantId,
                Sku = item.ProductVariant?.Sku ?? string.Empty,
                Name = item.ProductVariant?.Product?.Name ?? string.Empty,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice,
                LineTotal = item.LineTotal
            }).ToList(),
            DeliveryAddress = order.DeliveryAddress is null
                ? null
                : new OrderAddressResponse
                {
                    FullName = order.DeliveryAddress.FullName,
                    Line1 = order.DeliveryAddress.Line1,
                    Line2 = order.DeliveryAddress.Line2,
                    City = order.DeliveryAddress.City,
                    Province = order.DeliveryAddress.Province,
                    PostalCode = order.DeliveryAddress.PostalCode,
                    Country = order.DeliveryAddress.Country,
                    Phone = order.DeliveryAddress.Phone
                },
            Payments = order.Payments.Select(p => new PaymentResponse
            {
                Id = p.Id,
                Amount = p.Amount,
                Method = p.Method,
                Status = p.Status,
                TransactionReference = p.TransactionReference,
                PaidAt = p.PaidAt
            }).ToList(),
            Shipments = order.Shipments.Select(s => new ShipmentResponse
            {
                Id = s.Id,
                Status = s.Status,
                TrackingNumber = s.TrackingNumber,
                Carrier = s.Carrier,
                ShippedAt = s.ShippedAt,
                DeliveredAt = s.DeliveredAt
            }).ToList(),
            StatusHistory = order.StatusHistory
                .OrderBy(h => h.ChangedAt)
                .Select(h => new OrderStatusHistoryResponse
                {
                    Id = h.Id,
                    Status = h.Status,
                    ChangedAt = h.ChangedAt,
                    Note = h.Note
                })
                .ToList()
        };
    }
}
