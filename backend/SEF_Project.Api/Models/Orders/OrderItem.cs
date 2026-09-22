using SEF_Project.Api.Models.Catalog;

namespace SEF_Project.Api.Models.Orders;

public class OrderItem : GuidEntity
{
    public Guid OrderId { get; set; }

    public Order Order { get; set; } = null!;

    public Guid ProductVariantId { get; set; }

    public ProductVariant ProductVariant { get; set; } = null!;

    public int Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal LineTotal { get; set; }
}
