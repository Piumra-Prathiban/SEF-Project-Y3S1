using System.ComponentModel.DataAnnotations;

namespace SEF_Project.Api.DTOs.Catalog;

public class ProductSummaryResponse
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public Guid? SupplierId { get; set; }

    public string? SupplierName { get; set; }

    public bool IsActive { get; set; }

    public string? PrimaryImageUrl { get; set; }

    public decimal MinPrice { get; set; }

    public decimal MaxPrice { get; set; }

    public int VariantCount { get; set; }

    public int TotalQuantityOnHand { get; set; }

    public List<string> CategoryNames { get; set; } = new();

    public List<Guid> CategoryIds { get; set; } = new();

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}

public class ProductDetailResponse
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public Guid? SupplierId { get; set; }

    public string? SupplierName { get; set; }

    public bool IsActive { get; set; }

    public List<CategoryResponse> Categories { get; set; } = new();

    public List<ProductVariantResponse> Variants { get; set; } = new();

    public List<ProductImageResponse> Images { get; set; } = new();

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}

public class ProductQuery
{
    public string? Search { get; set; }

    public Guid? CategoryId { get; set; }

    public bool? IsActive { get; set; }

    [Range(1, 1000)]
    public int Page { get; set; } = 1;

    [Range(1, 100)]
    public int PageSize { get; set; } = 20;
}

public class ProductListResponse
{
    public List<ProductSummaryResponse> Items { get; set; } = new();

    public int TotalCount { get; set; }

    public int Page { get; set; }

    public int PageSize { get; set; }

    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
}

public class CreateProductRequest
{
    [Required]
    [StringLength(200, MinimumLength = 2)]
    public string Name { get; set; } = string.Empty;

    [StringLength(2000)]
    public string? Description { get; set; }

    public Guid? SupplierId { get; set; }

    public List<Guid> CategoryIds { get; set; } = new();

    public List<CreateProductVariantRequest>? InitialVariants { get; set; }

    public List<CreateProductImageRequest>? InitialImages { get; set; }

    public bool IsActive { get; set; } = true;
}

public class UpdateProductRequest
{
    [Required]
    [StringLength(200, MinimumLength = 2)]
    public string Name { get; set; } = string.Empty;

    [StringLength(2000)]
    public string? Description { get; set; }

    public Guid? SupplierId { get; set; }

    public List<Guid> CategoryIds { get; set; } = new();

    public bool IsActive { get; set; } = true;
}
