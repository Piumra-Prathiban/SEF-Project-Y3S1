using SEF_Project.Api.DTOs.Common;
using SEF_Project.Api.Models.Enums;

namespace SEF_Project.Api.DTOs.Analytics;

public class AnalyticsPagedResponse<T> : PagedResponse<T>
{
    public DateTime From { get; set; }

    public DateTime To { get; set; }
}

public class SalesSummaryResponse
{
    public DateTime From { get; set; }

    public DateTime To { get; set; }

    public int OrderCount { get; set; }

    public int UnitsSold { get; set; }

    /// <summary>Sum of order subtotals (before discounts).</summary>
    public decimal GrossSales { get; set; }

    public decimal DiscountTotal { get; set; }

    /// <summary>Sum of order totals.</summary>
    public decimal NetRevenue { get; set; }

    /// <summary>NetRevenue / OrderCount; 0 when there are no orders.</summary>
    public decimal AverageOrderValue { get; set; }

    public string Currency { get; set; } = string.Empty;

    /// <summary>Order statuses counted as sales.</summary>
    public List<OrderStatus> IncludedStatuses { get; set; } = new();
}

public class SalesOverTimeResponse
{
    public DateTime From { get; set; }

    public DateTime To { get; set; }

    public TimeGranularity Granularity { get; set; }

    public List<SalesTimePoint> Points { get; set; } = new();
}

public class SalesTimePoint
{
    /// <summary>Start of the UTC day or month.</summary>
    public DateTime PeriodStart { get; set; }

    public int OrderCount { get; set; }

    public int UnitsSold { get; set; }

    public decimal NetRevenue { get; set; }
}

public class ProductPerformanceItem
{
    public Guid ProductId { get; set; }

    public string ProductName { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    public int UnitsSold { get; set; }

    public int OrderCount { get; set; }

    /// <summary>Sum of OrderItem.LineTotal (price snapshots, before order-level discounts).</summary>
    public decimal Revenue { get; set; }
}

public class InventoryStockItem
{
    public Guid ProductVariantId { get; set; }

    public Guid ProductId { get; set; }

    public string ProductName { get; set; } = string.Empty;

    public string Sku { get; set; } = string.Empty;

    public string VariantName { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    public bool HasInventoryRecord { get; set; }

    public int QuantityOnHand { get; set; }

    public int ReservedQuantity { get; set; }

    /// <summary>QuantityOnHand - ReservedQuantity (derived, never stored).</summary>
    public int AvailableQuantity { get; set; }

    public int ReorderLevel { get; set; }

    public StockStatus StockStatus { get; set; }
}

public class InventorySummaryResponse
{
    public int VariantCount { get; set; }

    public int InStockCount { get; set; }

    public int LowStockCount { get; set; }

    public int OutOfStockCount { get; set; }

    public int TotalQuantityOnHand { get; set; }

    public int TotalReservedQuantity { get; set; }
}

public class PromotionPerformanceResponse : AnalyticsPagedResponse<PromotionPerformanceItem>
{
    /// <summary>Promotions live right now (active, within dates, campaign active).</summary>
    public int LivePromotionCount { get; set; }

    /// <summary>Coupon redemptions in the date range, across all promotions.</summary>
    public int TotalRedemptions { get; set; }
}

public class PromotionPerformanceItem
{
    public Guid PromotionId { get; set; }

    public string Name { get; set; } = string.Empty;

    public PromotionType Type { get; set; }

    public decimal DiscountValue { get; set; }

    public string? CampaignName { get; set; }

    public bool IsLive { get; set; }

    public int CouponCount { get; set; }

    /// <summary>Redemptions of this promotion's coupons in the date range.</summary>
    public int Redemptions { get; set; }

    public int UniqueCustomers { get; set; }

    /// <summary>Sales-status orders linked to a redemption in the date range.</summary>
    public int RedeemedOrderCount { get; set; }

    public decimal RedeemedOrderRevenue { get; set; }

    /// <summary>
    /// Sum of Order.DiscountTotal on those orders. Exact only when an order
    /// uses a single coupon; checkout does not record discounts yet.
    /// </summary>
    public decimal DiscountAmount { get; set; }
}

public class DemandItem
{
    public Guid ProductVariantId { get; set; }

    public Guid ProductId { get; set; }

    public string ProductName { get; set; } = string.Empty;

    public string Sku { get; set; } = string.Empty;

    public int UnitsSold { get; set; }

    /// <summary>Units sold in the preceding window of the same length.</summary>
    public int PreviousUnitsSold { get; set; }

    /// <summary>Sales velocity: UnitsSold / days in the range.</summary>
    public decimal UnitsPerDay { get; set; }

    /// <summary>% change vs the previous window; null when it had no sales.</summary>
    public decimal? TrendPercent { get; set; }

    public DemandTrend Trend { get; set; }

    public int AvailableQuantity { get; set; }

    /// <summary>AvailableQuantity / UnitsPerDay; null when there is no demand.</summary>
    public decimal? DaysOfCover { get; set; }
}
