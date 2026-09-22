namespace SEF_Project.Api.Models.Catalog;

public class ProductImage : GuidEntity
{
    public Guid ProductId { get; set; }

    public Product Product { get; set; } = null!;

    public string ImageUrl { get; set; } = string.Empty;

    public string? AltText { get; set; }

    public bool IsPrimary { get; set; }

    public int DisplayOrder { get; set; }
}
