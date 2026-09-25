namespace SEF_Project.Api.Models.Catalog;

public class ProductVariant : GuidEntity
{
    public Guid ProductId { get; set; }

    public Product Product { get; set; } = null!;

    public Guid SizeId { get; set; }

    public Size Size { get; set; } = null!;

    public Guid ColourId { get; set; }

    public Colour Colour { get; set; } = null!;

    public string Sku { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public decimal Price { get; set; }

    public bool IsActive { get; set; } = true;

    public InventoryStock? InventoryStock { get; set; }

    public ICollection<StockTransaction> StockTransactions { get; set; } =
        new List<StockTransaction>();
}
