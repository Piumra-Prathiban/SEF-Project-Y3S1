using SEF_Project.Api.Models.Enums;

namespace SEF_Project.Api.DTOs.Orders;

public class OrderListResponse
{
    public List<OrderSummaryResponse> Items { get; set; } = new();

    public int TotalCount { get; set; }

    public int Page { get; set; }

    public int PageSize { get; set; }
}

public class OrderSummaryResponse
{
    public Guid Id { get; set; }

    public string OrderNumber { get; set; } = string.Empty;

    public OrderStatus Status { get; set; }

    public DateTime PlacedAt { get; set; }

    public decimal Total { get; set; }

    public string Currency { get; set; } = string.Empty;
}
