using System.ComponentModel.DataAnnotations;

namespace SEF_Project.Api.DTOs.Catalog;

public class ProductImageResponse
{
    public Guid Id { get; set; }

    public Guid ProductId { get; set; }

    public string ImageUrl { get; set; } = string.Empty;

    public string? AltText { get; set; }

    public bool IsPrimary { get; set; }

    public int DisplayOrder { get; set; }

    public DateTime CreatedAt { get; set; }
}

public class CreateProductImageRequest
{
    [Required]
    [Url]
    [StringLength(1000)]
    public string ImageUrl { get; set; } = string.Empty;

    [StringLength(200)]
    public string? AltText { get; set; }

    public bool IsPrimary { get; set; } = false;

    [Range(0, 1000)]
    public int DisplayOrder { get; set; } = 0;
}

public class UpdateProductImageRequest
{
    [Required]
    [Url]
    [StringLength(1000)]
    public string ImageUrl { get; set; } = string.Empty;

    [StringLength(200)]
    public string? AltText { get; set; }

    public bool IsPrimary { get; set; }

    [Range(0, 1000)]
    public int DisplayOrder { get; set; }
}
