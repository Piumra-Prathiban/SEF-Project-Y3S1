namespace SEF_Project.Api.Models.Catalog;

public class Colour : GuidEntity
{
    public string Name { get; set; } = string.Empty;

    public string? HexCode { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<ProductVariant> ProductVariants { get; set; } =
        new List<ProductVariant>();
}
