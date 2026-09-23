using System.ComponentModel.DataAnnotations;
using SEF_Project.Api.DTOs.Common;
using SEF_Project.Api.Models.Enums;

namespace SEF_Project.Api.DTOs.Catalog;

public class StockAdjustmentDto
{
    [NotEmptyGuid]
    public Guid ProductVariantId { get; set; }

    [Required]
    [EnumDataType(typeof(InventoryTransactionType))]
    public InventoryTransactionType Type { get; set; }

    [Range(typeof(int), "-2147483648", "2147483647")]
    public int QuantityChange { get; set; }

    [StringLength(100)]
    public string? Reference { get; set; }

    [StringLength(500)]
    public string? Note { get; set; }
}

public class InventoryResponseDto
{
    public Guid Id { get; set; }

    public Guid ProductVariantId { get; set; }

    public string Sku { get; set; } = string.Empty;

    public string ProductName { get; set; } = string.Empty;

    public string VariantName { get; set; } = string.Empty;

    public int QuantityOnHand { get; set; }

    public int ReservedQuantity { get; set; }

    public int AvailableQuantity { get; set; }

    public int ReorderLevel { get; set; }

    public bool IsLowStock { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}

public class StockTransactionResponseDto
{
    public Guid Id { get; set; }

    public Guid ProductVariantId { get; set; }

    public string Sku { get; set; } = string.Empty;

    public InventoryTransactionType Type { get; set; }

    public int QuantityChange { get; set; }

    public int QuantityOnHandAfter { get; set; }

    public string? Reference { get; set; }

    public string? Note { get; set; }

    public DateTime CreatedAt { get; set; }
}
