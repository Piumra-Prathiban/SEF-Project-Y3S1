using System.Net;
using System.Net.Http.Json;
using SEF_Project.Api.DTOs.Analytics;

namespace SEF_Project.Api.Tests;

public class AnalyticsApiTests : IClassFixture<MarketingApiFactory>
{
    private readonly MarketingApiFactory _factory;

    public AnalyticsApiTests(MarketingApiFactory factory)
    {
        _factory = factory;
    }

    public static IEnumerable<object[]> Endpoints() => new[]
    {
        new object[] { "/api/analytics/sales/summary" },
        new object[] { "/api/analytics/sales/over-time?granularity=1" },
        new object[] { "/api/analytics/products/performance" },
        new object[] { "/api/analytics/inventory/stock" },
        new object[] { "/api/analytics/inventory/summary" },
        new object[] { "/api/analytics/promotions/performance" },
        new object[] { "/api/analytics/demand" }
    };

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task AnalyticsEndpoints_ShouldReturnOk_WhenStaff(string url)
    {
        var response = await _factory.CreateClientAs("Staff").GetAsync(url);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task AnalyticsEndpoints_ShouldReturnForbidden_WhenCustomer(string url)
    {
        var response = await _factory.CreateClientAs("Customer").GetAsync(url);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task AnalyticsEndpoints_ShouldReturnUnauthorized_WhenAnonymous(string url)
    {
        var response = await _factory.CreateClientAs(null).GetAsync(url);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/analytics/sales/summary?from=2026-10-11&to=2026-10-01")]
    [InlineData("/api/analytics/sales/summary?from=2020-01-01&to=2026-01-01")]
    [InlineData("/api/analytics/sales/over-time?from=2025-01-01&to=2026-06-01&granularity=0")]
    [InlineData("/api/analytics/products/performance?pageSize=500")]
    [InlineData("/api/analytics/inventory/stock?stockStatus=9")]
    public async Task AnalyticsEndpoints_ShouldReturnBadRequest_WhenQueryIsInvalid(string url)
    {
        var response = await _factory.CreateClientAs("Administrator").GetAsync(url);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task InventorySummary_ShouldReturnSeededStock()
    {
        var summary = await _factory.CreateClientAs("Staff")
            .GetFromJsonAsync<InventorySummaryResponse>("/api/analytics/inventory/summary");

        Assert.Equal(7, summary!.VariantCount);
        Assert.Equal(7, summary.InStockCount);
    }
}
