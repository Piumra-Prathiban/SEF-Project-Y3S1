using System.ComponentModel.DataAnnotations;
using SEF_Project.Api.Models.Enums;

namespace SEF_Project.Api.DTOs.Orders;

public class OrderQuery
{
    [Range(1, int.MaxValue)]
    public int Page { get; set; } = 1;

    [Range(1, 100)]
    public int PageSize { get; set; } = 20;

    public string? SortBy { get; set; }

    public string? SortDirection { get; set; }

    [EnumDataType(typeof(OrderStatus))]
    public OrderStatus? Status { get; set; }

    public DateTime? From { get; set; }

    public DateTime? To { get; set; }

    [StringLength(50)]
    public string? OrderNumber { get; set; }

    public int? CustomerId { get; set; }
}
