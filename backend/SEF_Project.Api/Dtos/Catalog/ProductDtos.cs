using System.ComponentModel.DataAnnotations;
using SEF_Project.Api.DTOs.Common;

namespace SEF_Project.Api.DTOs.Catalog;

public class ProductCreateDto
{
    [Required]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [StringLength(2000)]
    public string? Description { get; set; }

    [StringLength(500)]
    public string? ImageUrl { get; set; }

    [NotEmptyGuid]
    public Guid CategoryId { get; set; }

    [NotEmptyGuid]
    public Guid CollectionId { get; set; }

    public Guid? SupplierId { get; set; }

    public bool IsActive { get; set; } = true;
}

public class ProductUpdateDto
{
    [Required]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [StringLength(2000)]
    public string? Description { get; set; }

    [StringLength(500)]
    public string? ImageUrl { get; set; }

    [NotEmptyGuid]
    public Guid CategoryId { get; set; }

    [NotEmptyGuid]
    public Guid CollectionId { get; set; }

    public Guid? SupplierId { get; set; }

    public bool IsActive { get; set; } = true;
}

public class ProductResponseDto
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string? ImageUrl { get; set; }

    public Guid CategoryId { get; set; }

    public string CategoryName { get; set; } = string.Empty;

    public Guid CollectionId { get; set; }

    public string CollectionName { get; set; } = string.Empty;

    public Guid? SupplierId { get; set; }

    public string? SupplierName { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public List<ProductVariantResponseDto> Variants { get; set; } = new();
}

public class ProductQueryDto
{
    public string? Search { get; set; }

    public Guid? CategoryId { get; set; }

    public Guid? CollectionId { get; set; }

    public bool? IsActive { get; set; }

    public decimal? MinPrice { get; set; }

    public decimal? MaxPrice { get; set; }

    public string? SortBy { get; set; }

    public string? SortDirection { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 20;
}
