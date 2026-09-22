using System.ComponentModel.DataAnnotations;
using SEF_Project.Api.Models.Enums;

namespace SEF_Project.Api.DTOs.Inventory;

public class InventoryResponse
{
    public Guid Id { get; set; }

    public Guid ProductVariantId { get; set; }

    public Guid ProductId { get; set; }

    public string ProductName { get; set; } = string.Empty;

    public string VariantName { get; set; } = string.Empty;

    public string Sku { get; set; } = string.Empty;

    public string? Size { get; set; }

    public string? Colour { get; set; }

    public decimal Price { get; set; }

    public int QuantityOnHand { get; set; }

    public int ReservedQuantity { get; set; }

    public int AvailableQuantity { get; set; }

    public int ReorderLevel { get; set; }

    public bool IsLowStock { get; set; }

    public DateTime? LastUpdatedAt { get; set; }
}

public class InventoryQuery
{
    public string? Search { get; set; }

    public bool? LowStockOnly { get; set; }

    [Range(1, 1000)]
    public int Page { get; set; } = 1;

    [Range(1, 100)]
    public int PageSize { get; set; } = 20;
}

public class InventoryListResponse
{
    public List<InventoryResponse> Items { get; set; } = new();

    public int TotalCount { get; set; }

    public int Page { get; set; }

    public int PageSize { get; set; }

    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
}

public class StockAdjustmentRequest
{
    [Required]
    public Guid ProductVariantId { get; set; }

    [Required]
    public InventoryTransactionType Type { get; set; }

    /// <summary>
    /// For Receipt/Adjustment (add), positive number.
    /// For Sale/Transfer/Scrap (decrease), negative or positive number handled by type.
    /// </summary>
    [Required]
    public int QuantityChange { get; set; }

    [StringLength(100)]
    public string? Reference { get; set; }

    [StringLength(500)]
    public string? Note { get; set; }
}

public class InventoryTransactionResponse
{
    public Guid Id { get; set; }

    public Guid ProductVariantId { get; set; }

    public string Sku { get; set; } = string.Empty;

    public string VariantName { get; set; } = string.Empty;

    public string ProductName { get; set; } = string.Empty;

    public string Type { get; set; } = string.Empty;

    public int QuantityChange { get; set; }

    public int QuantityOnHandAfter { get; set; }

    public string? Reference { get; set; }

    public string? Note { get; set; }

    public DateTime CreatedAt { get; set; }
}
