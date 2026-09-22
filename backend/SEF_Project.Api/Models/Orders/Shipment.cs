using SEF_Project.Api.Models.Enums;

namespace SEF_Project.Api.Models.Orders;

public class Shipment : GuidEntity
{
    public Guid OrderId { get; set; }

    public Order Order { get; set; } = null!;

    public ShipmentStatus Status { get; set; }

    public string? TrackingNumber { get; set; }

    public string? Carrier { get; set; }

    public DateTime? ShippedAt { get; set; }

    public DateTime? DeliveredAt { get; set; }
}
