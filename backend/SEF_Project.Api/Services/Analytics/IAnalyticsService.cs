using SEF_Project.Api.DTOs.Analytics;

namespace SEF_Project.Api.Services.Analytics;

/// <summary>
/// Read-only Business Intelligence queries over the authoritative
/// transactional tables (orders, order items, inventory, promotions).
/// Invalid ranges throw <see cref="ArgumentException"/> (400).
/// </summary>
public interface IAnalyticsService
{
    Task<SalesSummaryResponse> GetSalesSummaryAsync(
        AnalyticsDateRangeQuery query,
        CancellationToken cancellationToken = default);

    Task<SalesOverTimeResponse> GetSalesOverTimeAsync(
        SalesOverTimeQuery query,
        CancellationToken cancellationToken = default);

    Task<AnalyticsPagedResponse<ProductPerformanceItem>> GetProductPerformanceAsync(
        ProductPerformanceQuery query,
        CancellationToken cancellationToken = default);

    Task<DTOs.Common.PagedResponse<InventoryStockItem>> GetInventoryStockAsync(
        InventoryStockQuery query,
        CancellationToken cancellationToken = default);

    Task<InventorySummaryResponse> GetInventorySummaryAsync(
        bool includeInactive,
        CancellationToken cancellationToken = default);

    Task<PromotionPerformanceResponse> GetPromotionPerformanceAsync(
        PromotionPerformanceQuery query,
        CancellationToken cancellationToken = default);

    Task<AnalyticsPagedResponse<DemandItem>> GetDemandInsightsAsync(
        DemandQuery query,
        CancellationToken cancellationToken = default);
}
