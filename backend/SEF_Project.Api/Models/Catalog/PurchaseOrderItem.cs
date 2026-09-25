namespace SEF_Project.Api.Models.Catalog;

public class PurchaseOrderItem : GuidEntity
{
    public Guid PurchaseOrderId { get; set; }

    public PurchaseOrder PurchaseOrder { get; set; } = null!;

    public Guid ProductVariantId { get; set; }

    public ProductVariant ProductVariant { get; set; } = null!;

    public int Quantity { get; set; }

    public decimal UnitCost { get; set; }
}
