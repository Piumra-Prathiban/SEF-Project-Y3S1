namespace SEF_Project.Api.Models.Catalog;

public class Product : GuidEntity
{
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public Guid CategoryId { get; set; }

    public Category Category { get; set; } = null!;

    public Guid CollectionId { get; set; }

    public Collection Collection { get; set; } = null!;

    public Guid? SupplierId { get; set; }

    public Supplier? Supplier { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<ProductVariant> Variants { get; set; } =
        new List<ProductVariant>();
}
