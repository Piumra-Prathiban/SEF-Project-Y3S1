namespace SEF_Project.Api.Models.Orders;

public class ReturnItem : GuidEntity
{
    public Guid ProductReturnId { get; set; }

    public ProductReturn ProductReturn { get; set; } = null!;

    public Guid OrderItemId { get; set; }

    public OrderItem OrderItem { get; set; } = null!;

    public int Quantity { get; set; }

    public decimal UnitRefundAmount { get; set; }
}
