using SEF_Project.Api.Models.Enums;
using SEF_Project.Api.Models.Marketing;

namespace SEF_Project.Api.Models.Orders;

public class Order : GuidEntity
{
    public string OrderNumber { get; set; } = string.Empty;

    public int CustomerId { get; set; }

    public Customer Customer { get; set; } = null!;

    public OrderStatus Status { get; set; }

    public decimal Subtotal { get; set; }

    public decimal DiscountTotal { get; set; }

    public decimal TaxAmount { get; set; }

    public decimal ShippingFee { get; set; }

    public decimal Total { get; set; }

    public string Currency { get; set; } = "LKR";

    public DateTime PlacedAt { get; set; }

    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();

    public ICollection<OrderStatusHistory> StatusHistory { get; set; } =
        new List<OrderStatusHistory>();

    public ICollection<Payment> Payments { get; set; } = new List<Payment>();

    public ICollection<Shipment> Shipments { get; set; } = new List<Shipment>();

    public ICollection<ProductReturn> Returns { get; set; } =
        new List<ProductReturn>();

    // In practice at most one: CreateOrderRequest accepts a single
    // CouponCode. A collection mirrors the CouponRedemption side's
    // unrestricted WithMany() and costs nothing extra to keep accurate.
    public ICollection<CouponRedemption> CouponRedemptions { get; set; } =
        new List<CouponRedemption>();

    public OrderAddress? DeliveryAddress { get; set; }
}
