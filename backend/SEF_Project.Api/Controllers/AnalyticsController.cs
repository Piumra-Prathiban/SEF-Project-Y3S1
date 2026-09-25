using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SEF_Project.Api.DTOs.Analytics;
using SEF_Project.Api.DTOs.Common;
using SEF_Project.Api.Services.Analytics;

namespace SEF_Project.Api.Controllers;

/// <summary>
/// Business Intelligence reports. Date ranges are half-open UTC [from, to)
/// and default to the last 30 days.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Staff,Administrator")]
[Produces("application/json")]
[ProducesResponseType(StatusCodes.Status200OK)]
[ProducesResponseType(StatusCodes.Status400BadRequest)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
public class AnalyticsController : ControllerBase
{
    private readonly IAnalyticsService _analyticsService;

    public AnalyticsController(IAnalyticsService analyticsService)
    {
        _analyticsService = analyticsService;
    }

    [HttpGet("sales/summary")]
    public async Task<ActionResult<SalesSummaryResponse>> GetSalesSummary(
        [FromQuery] AnalyticsDateRangeQuery query,
        CancellationToken cancellationToken) =>
        Ok(await _analyticsService.GetSalesSummaryAsync(query, cancellationToken));

    [HttpGet("sales/over-time")]
    public async Task<ActionResult<SalesOverTimeResponse>> GetSalesOverTime(
        [FromQuery] SalesOverTimeQuery query,
        CancellationToken cancellationToken) =>
        Ok(await _analyticsService.GetSalesOverTimeAsync(query, cancellationToken));

    /// <summary>Top sellers (sortDirection=desc) or low performers (asc).</summary>
    [HttpGet("products/performance")]
    public async Task<ActionResult<AnalyticsPagedResponse<ProductPerformanceItem>>> GetProductPerformance(
        [FromQuery] ProductPerformanceQuery query,
        CancellationToken cancellationToken) =>
        Ok(await _analyticsService.GetProductPerformanceAsync(query, cancellationToken));

    [HttpGet("inventory/stock")]
    public async Task<ActionResult<PagedResponse<InventoryStockItem>>> GetInventoryStock(
        [FromQuery] InventoryStockQuery query,
        CancellationToken cancellationToken) =>
        Ok(await _analyticsService.GetInventoryStockAsync(query, cancellationToken));

    [HttpGet("inventory/summary")]
    public async Task<ActionResult<InventorySummaryResponse>> GetInventorySummary(
        [FromQuery] bool includeInactive,
        CancellationToken cancellationToken) =>
        Ok(await _analyticsService.GetInventorySummaryAsync(includeInactive, cancellationToken));

    [HttpGet("promotions/performance")]
    public async Task<ActionResult<PromotionPerformanceResponse>> GetPromotionPerformance(
        [FromQuery] PromotionPerformanceQuery query,
        CancellationToken cancellationToken) =>
        Ok(await _analyticsService.GetPromotionPerformanceAsync(query, cancellationToken));

    /// <summary>High demand (sortDirection=desc) or low demand (asc).</summary>
    [HttpGet("demand")]
    public async Task<ActionResult<AnalyticsPagedResponse<DemandItem>>> GetDemandInsights(
        [FromQuery] DemandQuery query,
        CancellationToken cancellationToken) =>
        Ok(await _analyticsService.GetDemandInsightsAsync(query, cancellationToken));
}
