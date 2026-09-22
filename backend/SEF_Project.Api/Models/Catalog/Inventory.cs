namespace SEF_Project.Api.Models.Catalog;

public class Inventory : GuidEntity
{
    public Guid ProductVariantId { get; set; }

    public ProductVariant ProductVariant { get; set; } = null!;

    public int QuantityOnHand { get; set; }

    public int ReservedQuantity { get; set; }

    public int ReorderLevel { get; set; }
}
