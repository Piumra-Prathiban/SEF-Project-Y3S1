using System.ComponentModel.DataAnnotations;

namespace SEF_Project.Api.DTOs.Catalog;

public class ProductVariantResponse
{
    public Guid Id { get; set; }

    public Guid ProductId { get; set; }

    public string Sku { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Size { get; set; }

    public string? Colour { get; set; }

    public decimal Price { get; set; }

    public bool IsActive { get; set; }

    public int QuantityOnHand { get; set; }

    public int ReservedQuantity { get; set; }

    public int AvailableQuantity { get; set; }

    public int ReorderLevel { get; set; }

    public bool IsLowStock { get; set; }
}

public class CreateProductVariantRequest
{
    [Required]
    [StringLength(100, MinimumLength = 2)]
    public string Sku { get; set; } = string.Empty;

    [Required]
    [StringLength(200, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    [StringLength(50)]
    public string? Size { get; set; }

    [StringLength(50)]
    public string? Colour { get; set; }

    [Range(0.01, 1000000.00)]
    public decimal Price { get; set; }

    [Range(0, 100000)]
    public int InitialStock { get; set; } = 0;

    [Range(0, 100000)]
    public int ReorderLevel { get; set; } = 10;

    public bool IsActive { get; set; } = true;
}

public class UpdateProductVariantRequest
{
    [Required]
    [StringLength(100, MinimumLength = 2)]
    public string Sku { get; set; } = string.Empty;

    [Required]
    [StringLength(200, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    [StringLength(50)]
    public string? Size { get; set; }

    [StringLength(50)]
    public string? Colour { get; set; }

    [Range(0.01, 1000000.00)]
    public decimal Price { get; set; }

    [Range(0, 100000)]
    public int ReorderLevel { get; set; } = 10;

    public bool IsActive { get; set; } = true;
}
