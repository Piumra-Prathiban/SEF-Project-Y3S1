namespace SEF_Project.Api.Models.Catalog;

public class Category : GuidEntity
{
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<ProductCategory> ProductCategories { get; set; } =
        new List<ProductCategory>();
}
