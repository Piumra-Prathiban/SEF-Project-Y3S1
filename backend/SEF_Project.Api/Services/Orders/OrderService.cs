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
        var order = await LoadFullOrderAsync(orderId, cancellationToken);

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

    private async Task<Order?> LoadFullOrderAsync(
        Guid orderId,
        CancellationToken cancellationToken)
    {
        return await _context.Orders
            .AsNoTracking()
            .Include(o => o.Customer)
            .Include(o => o.Items)
                .ThenInclude(i => i.ProductVariant)
                .ThenInclude(v => v.Product)
            .Include(o => o.DeliveryAddress)
            .Include(o => o.Payments)
            .Include(o => o.Shipments)
            .Include(o => o.StatusHistory)
            .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);
    }

    private static bool CanView(
        Order order,
        int userId,
        bool canAccessAllOrders) =>
        canAccessAllOrders || order.Customer.UserId == userId;

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
