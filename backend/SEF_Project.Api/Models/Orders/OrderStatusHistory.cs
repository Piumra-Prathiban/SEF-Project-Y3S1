using SEF_Project.Api.Models.Enums;

namespace SEF_Project.Api.Models.Orders;

public class OrderStatusHistory : GuidEntity
{
    public Guid OrderId { get; set; }

    public Order Order { get; set; } = null!;

    public OrderStatus Status { get; set; }

    public DateTime ChangedAt { get; set; }

    public int? ChangedByUserId { get; set; }

    public User? ChangedByUser { get; set; }

    public string? Note { get; set; }
}
