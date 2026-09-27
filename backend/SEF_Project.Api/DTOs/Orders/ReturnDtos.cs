using System.ComponentModel.DataAnnotations;
using SEF_Project.Api.Models.Enums;

namespace SEF_Project.Api.DTOs.Orders;

public class CreateReturnRequest
{
    [EnumDataType(typeof(ReturnReason))]
    public ReturnReason Reason { get; set; }

    [StringLength(1000)]
    public string? Note { get; set; }

    [Required, MinLength(1)]
    public List<CreateReturnItemRequest> Items { get; set; } = new();
}

public class CreateReturnItemRequest
{
    public Guid OrderItemId { get; set; }

    [Range(1, int.MaxValue)]
    public int Quantity { get; set; }
}

public class UpdateReturnStatusRequest
{
    [EnumDataType(typeof(ReturnStatus))]
    public ReturnStatus Status { get; set; }

    [StringLength(1000)]
    public string? Note { get; set; }
}

public class CancelReturnRequest
{
    [StringLength(1000)]
    public string? Note { get; set; }
}

public class ReturnQuery
{
    [Range(1, 10000)]
    public int Page { get; set; } = 1;

    [Range(1, 100)]
    public int PageSize { get; set; } = 20;

    [EnumDataType(typeof(ReturnStatus))]
    public ReturnStatus? Status { get; set; }

    [StringLength(50)]
    public string? OrderNumber { get; set; }
}

public class ReturnListResponse
{
    public List<ReturnResponse> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

public class ReturnResponse
{
    public Guid Id { get; set; }
    public string ReturnNumber { get; set; } = string.Empty;
    public Guid OrderId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public ReturnStatus Status { get; set; }
    public ReturnReason Reason { get; set; }
    public string? CustomerNote { get; set; }
    public string? StaffNote { get; set; }
    public decimal RefundAmount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public DateTime RequestedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public DateTime? ReceivedAt { get; set; }
    public DateTime? RefundedAt { get; set; }
    public List<ReturnItemResponse> Items { get; set; } = new();
}

public class ReturnItemResponse
{
    public Guid Id { get; set; }
    public Guid OrderItemId { get; set; }
    public Guid ProductVariantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitRefundAmount { get; set; }
    public decimal LineRefundAmount { get; set; }
}
