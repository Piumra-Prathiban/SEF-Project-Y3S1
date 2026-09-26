using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using SEF_Project.Api.DTOs.Common;
using SEF_Project.Api.Models.Enums;

namespace SEF_Project.Api.DTOs.Catalog;

public class StockAdjustmentDto
{
    public Guid ProductVariantId { get; set; }

    [Required]
    [JsonRequired]
    [EnumDataType(typeof(InventoryTransactionType))]
    public InventoryTransactionType Type { get; set; }

    [Range(1, int.MaxValue)]
    public int Quantity { get; set; }

    [StringLength(100)]
    public string? Reference { get; set; }

    [Required]
    [StringLength(500)]
    public string Reason { get; set; } = string.Empty;
}

public class InventoryResponseDto
{
    public Guid Id { get; set; }

    public Guid ProductVariantId { get; set; }

    public Guid ProductId { get; set; }

    public Guid CategoryId { get; set; }

    public string Sku { get; set; } = string.Empty;

    public string ProductName { get; set; } = string.Empty;

    public string VariantName { get; set; } = string.Empty;

    public string SizeName { get; set; } = string.Empty;

    public string ColourName { get; set; } = string.Empty;

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

    public int Quantity { get; set; }

    public int QuantityChange { get; set; }

    public int PreviousQuantityOnHand { get; set; }

    public int QuantityOnHandAfter { get; set; }

    public int? PerformedByUserId { get; set; }

    public string? PerformedByUserEmail { get; set; }

    public string? Reference { get; set; }

    public string? Reason { get; set; }

    public DateTime CreatedAt { get; set; }
}
