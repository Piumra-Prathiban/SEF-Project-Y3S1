namespace SEF_Project.Api.Models.Catalog;

public class ProductVariant : GuidEntity
{
    public Guid ProductId { get; set; }

    public Product Product { get; set; } = null!;

    public string Sku { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public decimal Price { get; set; }

    public bool IsActive { get; set; } = true;

    public Inventory? Inventory { get; set; }

    public ICollection<InventoryTransaction> InventoryTransactions { get; set; } =
        new List<InventoryTransaction>();
}
