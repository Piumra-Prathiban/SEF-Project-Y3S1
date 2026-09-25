using System.ComponentModel.DataAnnotations;

namespace SEF_Project.Api.DTOs.Analytics;

public enum TimeGranularity
{
    Day,
    Month
}

public enum StockStatus
{
    InStock,
    LowStock,
    OutOfStock
}

public enum DemandTrend
{
    NoSales,
    New,
    Rising,
    Stable,
    Falling
}

/// <summary>
/// Half-open UTC range [From, To). Defaults to the last 30 days ending now.
/// </summary>
public class AnalyticsDateRangeQuery : IValidatableObject
{
    public DateTime? From { get; set; }

    public DateTime? To { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (From is not null && To is not null && To <= From)
        {
            yield return new ValidationResult(
                "'to' must be after 'from'.",
                new[] { nameof(To) });
        }
    }
}

public class SalesOverTimeQuery : AnalyticsDateRangeQuery
{
    [EnumDataType(typeof(TimeGranularity))]
    public TimeGranularity Granularity { get; set; } = TimeGranularity.Day;
}

public class ProductPerformanceQuery : AnalyticsDateRangeQuery
{
    [Range(1, int.MaxValue)]
    public int Page { get; set; } = 1;

    [Range(1, 100)]
    public int PageSize { get; set; } = 20;

    /// <summary>unitsSold (default), revenue, orderCount or name.</summary>
    [StringLength(50)]
    public string? SortBy { get; set; }

    /// <summary>desc (default) shows top sellers; asc shows low performers.</summary>
    [StringLength(4)]
    public string? SortDirection { get; set; }

    public bool IncludeInactive { get; set; }
}

public class InventoryStockQuery
{
    [Range(1, int.MaxValue)]
    public int Page { get; set; } = 1;

    [Range(1, 100)]
    public int PageSize { get; set; } = 20;

    /// <summary>availableQuantity (default), quantityOnHand, sku or productName.</summary>
    [StringLength(50)]
    public string? SortBy { get; set; }

    /// <summary>asc (default) or desc.</summary>
    [StringLength(4)]
    public string? SortDirection { get; set; }

    [EnumDataType(typeof(StockStatus))]
    public StockStatus? StockStatus { get; set; }

    [StringLength(200)]
    public string? Search { get; set; }

    public bool IncludeInactive { get; set; }
}

public class PromotionPerformanceQuery : AnalyticsDateRangeQuery
{
    [Range(1, int.MaxValue)]
    public int Page { get; set; } = 1;

    [Range(1, 100)]
    public int PageSize { get; set; } = 20;

    /// <summary>redemptions (default), revenue, discountAmount or name.</summary>
    [StringLength(50)]
    public string? SortBy { get; set; }

    /// <summary>asc or desc (default).</summary>
    [StringLength(4)]
    public string? SortDirection { get; set; }

    public bool LiveOnly { get; set; }
}

public class DemandQuery : AnalyticsDateRangeQuery
{
    [Range(1, int.MaxValue)]
    public int Page { get; set; } = 1;

    [Range(1, 100)]
    public int PageSize { get; set; } = 20;

    /// <summary>unitsSold (default, i.e. velocity), trend, availableQuantity or sku.</summary>
    [StringLength(50)]
    public string? SortBy { get; set; }

    /// <summary>desc (default) shows high demand; asc shows low demand.</summary>
    [StringLength(4)]
    public string? SortDirection { get; set; }

    public bool IncludeInactive { get; set; }
}
