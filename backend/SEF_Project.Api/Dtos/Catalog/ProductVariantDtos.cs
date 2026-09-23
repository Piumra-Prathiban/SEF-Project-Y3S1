using System.ComponentModel.DataAnnotations;
using SEF_Project.Api.DTOs.Common;

namespace SEF_Project.Api.DTOs.Catalog;

public class ProductVariantCreateDto
{
    public Guid ProductId { get; set; }

    [NotEmptyGuid]
    public Guid SizeId { get; set; }

    [NotEmptyGuid]
    public Guid ColourId { get; set; }

    [Required]
    [StringLength(100)]
    public string Sku { get; set; } = string.Empty;

    [Required]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [Range(0, double.MaxValue)]
    public decimal Price { get; set; }

    public bool IsActive { get; set; } = true;

    [Range(0, int.MaxValue)]
    public int InitialQuantityOnHand { get; set; }

    [Range(0, int.MaxValue)]
    public int ReorderLevel { get; set; }
}

public class ProductVariantUpdateDto
{
    [NotEmptyGuid]
    public Guid SizeId { get; set; }

    [NotEmptyGuid]
    public Guid ColourId { get; set; }

    [Required]
    [StringLength(100)]
    public string Sku { get; set; } = string.Empty;

    [Required]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [Range(0, double.MaxValue)]
    public decimal Price { get; set; }

    public bool IsActive { get; set; } = true;

    [Range(0, int.MaxValue)]
    public int ReorderLevel { get; set; }
}

public class ProductVariantResponseDto
{
    public Guid Id { get; set; }

    public Guid ProductId { get; set; }

    public string ProductName { get; set; } = string.Empty;

    public Guid SizeId { get; set; }

    public string SizeName { get; set; } = string.Empty;

    public Guid ColourId { get; set; }

    public string ColourName { get; set; } = string.Empty;

    public string Sku { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public decimal Price { get; set; }

    public bool IsActive { get; set; }

    public InventoryResponseDto? Inventory { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
