using SEF_Project.Api.Models.Enums;

namespace SEF_Project.Api.DTOs.Orders;

public class OrderResponse
{
    public Guid Id { get; set; }

    public string OrderNumber { get; set; } = string.Empty;

    public OrderStatus Status { get; set; }

    public DateTime PlacedAt { get; set; }

    public decimal Subtotal { get; set; }

    public decimal DiscountTotal { get; set; }

    public decimal TaxAmount { get; set; }

    public decimal ShippingFee { get; set; }

    public decimal Total { get; set; }

    public string Currency { get; set; } = string.Empty;

    public List<OrderItemResponse> Items { get; set; } = new();

    public OrderAddressResponse? DeliveryAddress { get; set; }

    public List<PaymentResponse> Payments { get; set; } = new();

    public List<ShipmentResponse> Shipments { get; set; } = new();

    public List<OrderStatusHistoryResponse> StatusHistory { get; set; } = new();
}

public class OrderItemResponse
{
    public Guid Id { get; set; }

    public Guid ProductVariantId { get; set; }

    public string Sku { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public int Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal LineTotal { get; set; }
}

public class OrderAddressResponse
{
    public string FullName { get; set; } = string.Empty;

    public string Line1 { get; set; } = string.Empty;

    public string? Line2 { get; set; }

    public string City { get; set; } = string.Empty;

    public string? Province { get; set; }

    public string PostalCode { get; set; } = string.Empty;

    public string Country { get; set; } = string.Empty;

    public string? Phone { get; set; }
}

public class PaymentResponse
{
    public Guid Id { get; set; }

    public decimal Amount { get; set; }

    public PaymentMethod Method { get; set; }

    public PaymentStatus Status { get; set; }

    public string? TransactionReference { get; set; }

    public DateTime? PaidAt { get; set; }
}

public class ShipmentResponse
{
    public Guid Id { get; set; }

    public ShipmentStatus Status { get; set; }

    public string? TrackingNumber { get; set; }

    public string? Carrier { get; set; }

    public DateTime? ShippedAt { get; set; }

    public DateTime? DeliveredAt { get; set; }
}

public class OrderStatusHistoryResponse
{
    public Guid Id { get; set; }

    public OrderStatus Status { get; set; }

    public DateTime ChangedAt { get; set; }

    public string? Note { get; set; }
}
