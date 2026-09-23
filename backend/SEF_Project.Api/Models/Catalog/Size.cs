namespace SEF_Project.Api.Models.Catalog;

public class Size : GuidEntity
{
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public int DisplayOrder { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<ProductVariant> ProductVariants { get; set; } =
        new List<ProductVariant>();
}
