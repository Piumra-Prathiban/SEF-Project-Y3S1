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
                    ProductVariantId = item.ProductVariantId,
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

            return BuildResponse(order, variants);
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

    private static OrderResponse BuildResponse(
        Order order,
        Dictionary<Guid, ProductVariant> variants)
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
            Items = order.Items.Select(item =>
            {
                variants.TryGetValue(item.ProductVariantId, out var variant);
                return new OrderItemResponse
                {
                    Id = item.Id,
                    ProductVariantId = item.ProductVariantId,
                    Sku = variant?.Sku ?? string.Empty,
                    Name = variant?.Product.Name ?? string.Empty,
                    Quantity = item.Quantity,
                    UnitPrice = item.UnitPrice,
                    LineTotal = item.LineTotal
                };
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
            Payments = new List<PaymentResponse>(),
            Shipments = new List<ShipmentResponse>(),
            StatusHistory = order.StatusHistory.Select(h =>
                new OrderStatusHistoryResponse
                {
                    Id = h.Id,
                    Status = h.Status,
                    ChangedAt = h.ChangedAt,
                    Note = h.Note
                }).ToList()
        };
    }
}
