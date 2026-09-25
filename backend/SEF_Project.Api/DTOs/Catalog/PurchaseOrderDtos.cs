using System.ComponentModel.DataAnnotations;
using SEF_Project.Api.DTOs.Common;
using SEF_Project.Api.Models.Enums;

namespace SEF_Project.Api.DTOs.Catalog;

/// <summary>Payload for raising a new (draft) purchase order against a supplier.</summary>
public class PurchaseOrderCreateRequest
{
    [NotEmptyGuid]
    public Guid SupplierId { get; set; }

    public DateTime? ExpectedAt { get; set; }

    [StringLength(2000)]
    public string? Notes { get; set; }

    [Required]
    [MinLength(1)]
    public List<PurchaseOrderItemRequest> Items { get; set; } = new();
}

/// <summary>A single restock line on a purchase order.</summary>
public class PurchaseOrderItemRequest
{
    [NotEmptyGuid]
    public Guid ProductVariantId { get; set; }

    [Range(1, int.MaxValue)]
    public int Quantity { get; set; }

    [Range(0, double.MaxValue)]
    public decimal UnitCost { get; set; }
}

/// <summary>A purchase-order line as returned by the API.</summary>
public class PurchaseOrderItemResponse
{
    public Guid Id { get; set; }

    public Guid ProductVariantId { get; set; }

    public string Sku { get; set; } = string.Empty;

    public string ProductName { get; set; } = string.Empty;

    public string VariantName { get; set; } = string.Empty;

    public int Quantity { get; set; }

    public decimal UnitCost { get; set; }

    /// <summary>Computed as <c>Quantity * UnitCost</c>.</summary>
    public decimal LineTotal { get; set; }
}

/// <summary>A purchase order as returned by the API.</summary>
public class PurchaseOrderResponse
{
    public Guid Id { get; set; }

    public string OrderNumber { get; set; } = string.Empty;

    public Guid SupplierId { get; set; }

    public string SupplierName { get; set; } = string.Empty;

    public PurchaseOrderStatus Status { get; set; }

    public DateTime? ExpectedAt { get; set; }

    public DateTime? SubmittedAt { get; set; }

    public DateTime? ReceivedAt { get; set; }

    public string? Notes { get; set; }

    public List<PurchaseOrderItemResponse> Items { get; set; } = new();

    /// <summary>Computed as the sum of every line's <see cref="PurchaseOrderItemResponse.LineTotal"/>.</summary>
    public decimal Total { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}

/// <summary>Paging and filters applied when listing purchase orders.</summary>
public class PurchaseOrderQueryDto
{
    [Range(1, int.MaxValue)]
    public int Page { get; set; } = 1;

    [Range(1, 100)]
    public int PageSize { get; set; } = 20;

    public Guid? SupplierId { get; set; }

    public PurchaseOrderStatus? Status { get; set; }
}
