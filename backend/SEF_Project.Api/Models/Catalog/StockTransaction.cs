using SEF_Project.Api.Models.Enums;

namespace SEF_Project.Api.Models.Catalog;

public class StockTransaction : GuidEntity
{
    public Guid ProductVariantId { get; set; }

    public ProductVariant ProductVariant { get; set; } = null!;

    public InventoryTransactionType Type { get; set; }

    public int QuantityChange { get; set; }

    public int QuantityOnHandBefore { get; set; }

    public int QuantityOnHandAfter { get; set; }

    public int? PerformedByUserId { get; set; }

    public User? PerformedByUser { get; set; }

    public string? Reference { get; set; }

    public string? Note { get; set; }
}
