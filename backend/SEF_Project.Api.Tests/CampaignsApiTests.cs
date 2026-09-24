using System.Net;
using System.Net.Http.Json;
using SEF_Project.Api.Data.Configurations;
using SEF_Project.Api.DTOs.Common;
using SEF_Project.Api.DTOs.Marketing;
using SEF_Project.Api.Models.Enums;

namespace SEF_Project.Api.Tests;

public class CampaignsApiTests : IClassFixture<MarketingApiFactory>
{
    private const string Url = "/api/campaigns";

    private readonly MarketingApiFactory _factory;

    public CampaignsApiTests(MarketingApiFactory factory)
    {
        _factory = factory;
    }

    private static CampaignRequest ValidRequest(
        string name = "Test Campaign",
        CampaignStatus status = CampaignStatus.Draft) =>
        new()
        {
            Name = name,
            Description = "Created by an API test.",
            StartDate = new DateTime(2027, 4, 1, 0, 0, 0, DateTimeKind.Utc),
            EndDate = new DateTime(2027, 4, 30, 0, 0, 0, DateTimeKind.Utc),
            Status = status
        };

    private async Task<CampaignResponse> CreateAsStaffAsync(CampaignRequest request)
    {
        var response = await _factory.CreateClientAs("Staff").PostAsJsonAsync(Url, request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return (await response.Content.ReadFromJsonAsync<CampaignResponse>())!;
    }

    [Fact]
    public async Task GetCampaigns_ShouldReturnCampaigns_WhenStaff()
    {
        var response = await _factory.CreateClientAs("Staff").GetAsync($"{Url}?pageSize=100");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var page = await response.Content.ReadFromJsonAsync<PagedResponse<CampaignResponse>>();
        var summer = page!.Items.Single(c => c.Id == SeedData.CampaignSummer);

        Assert.Equal(CampaignStatus.Active, summer.Status);
        Assert.Equal(2, summer.PromotionCount);
    }

    [Fact]
    public async Task GetCampaigns_ShouldFilterByStatus_WhenStatusIsProvided()
    {
        var response = await _factory.CreateClientAs("Administrator")
            .GetAsync($"{Url}?status={(int)CampaignStatus.Scheduled}");

        var page = await response.Content.ReadFromJsonAsync<PagedResponse<CampaignResponse>>();

        Assert.Contains(page!.Items, c => c.Id == SeedData.CampaignWeekendRefresh);
        Assert.All(page.Items, c => Assert.Equal(CampaignStatus.Scheduled, c.Status));
    }

    [Fact]
    public async Task GetCampaigns_ShouldReturnUnauthorized_WhenAnonymous()
    {
        var response = await _factory.CreateClientAs(null).GetAsync(Url);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CampaignEndpoints_ShouldReturnForbidden_WhenCustomer()
    {
        var client = _factory.CreateClientAs("Customer");

        var list = await client.GetAsync(Url);
        var create = await client.PostAsJsonAsync(Url, ValidRequest());
        var delete = await client.DeleteAsync($"{Url}/{SeedData.CampaignSummer}");

        Assert.Equal(HttpStatusCode.Forbidden, list.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, create.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, delete.StatusCode);
    }

    [Fact]
    public async Task GetCampaignById_ShouldReturnNotFound_WhenMissing()
    {
        var response = await _factory.CreateClientAs("Staff").GetAsync($"{Url}/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreateCampaign_ShouldReturnCreated_WhenValid()
    {
        var response = await _factory.CreateClientAs("Staff")
            .PostAsJsonAsync(Url, ValidRequest("Spring Specials", CampaignStatus.Scheduled));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);

        var created = await response.Content.ReadFromJsonAsync<CampaignResponse>();

        Assert.Equal("Spring Specials", created!.Name);
        Assert.Equal(CampaignStatus.Scheduled, created.Status);
        Assert.Equal(0, created.PromotionCount);
    }

    [Fact]
    public async Task CreateCampaign_ShouldReturnBadRequest_WhenEndDateIsBeforeStartDate()
    {
        var request = ValidRequest();
        request.EndDate = request.StartDate!.Value.AddDays(-1);

        var response = await _factory.CreateClientAs("Staff").PostAsJsonAsync(Url, request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateCampaign_ShouldReturnBadRequest_WhenNameIsMissing()
    {
        var request = ValidRequest();
        request.Name = "";

        var response = await _factory.CreateClientAs("Staff").PostAsJsonAsync(Url, request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateCampaign_ShouldReturnBadRequest_WhenStatusIsInvalid()
    {
        var response = await _factory.CreateClientAs("Staff").PostAsJsonAsync(Url, new
        {
            name = "Bad Status",
            startDate = "2027-04-01T00:00:00Z",
            endDate = "2027-04-30T00:00:00Z",
            status = 99
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateCampaign_ShouldReturnUpdatedCampaign_WhenValid()
    {
        var created = await CreateAsStaffAsync(ValidRequest("Draft Campaign"));

        var response = await _factory.CreateClientAs("Staff").PutAsJsonAsync(
            $"{Url}/{created.Id}",
            ValidRequest("Now Active", CampaignStatus.Active));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var updated = await response.Content.ReadFromJsonAsync<CampaignResponse>();

        Assert.Equal("Now Active", updated!.Name);
        Assert.Equal(CampaignStatus.Active, updated.Status);
    }

    [Fact]
    public async Task UpdateCampaign_ShouldReturnNotFound_WhenMissing()
    {
        var response = await _factory.CreateClientAs("Staff")
            .PutAsJsonAsync($"{Url}/{Guid.NewGuid()}", ValidRequest());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpdateCampaign_ShouldReturnConflict_WhenReopeningCompletedCampaign()
    {
        var created = await CreateAsStaffAsync(ValidRequest("Finished", CampaignStatus.Completed));

        var response = await _factory.CreateClientAs("Staff").PutAsJsonAsync(
            $"{Url}/{created.Id}",
            ValidRequest("Finished", CampaignStatus.Active));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task UpdateCampaign_ShouldReturnConflict_WhenDatesNoLongerCoverPromotions()
    {
        // Summer Launch promotions run until 2026-12-31.
        var request = new CampaignRequest
        {
            Name = "Summer Launch",
            StartDate = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc),
            EndDate = new DateTime(2026, 10, 31, 0, 0, 0, DateTimeKind.Utc),
            Status = CampaignStatus.Active
        };

        var response = await _factory.CreateClientAs("Staff")
            .PutAsJsonAsync($"{Url}/{SeedData.CampaignSummer}", request);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task DeleteCampaign_ShouldReturnNoContent_WhenCampaignHasNoPromotions()
    {
        var created = await CreateAsStaffAsync(ValidRequest("Short Lived"));
        var client = _factory.CreateClientAs("Administrator");

        var delete = await client.DeleteAsync($"{Url}/{created.Id}");
        var get = await client.GetAsync($"{Url}/{created.Id}");

        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
    }

    [Fact]
    public async Task DeleteCampaign_ShouldReturnConflict_WhenCampaignHasPromotions()
    {
        var response = await _factory.CreateClientAs("Staff")
            .DeleteAsync($"{Url}/{SeedData.CampaignSummer}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task DeleteCampaign_ShouldReturnNotFound_WhenMissing()
    {
        var response = await _factory.CreateClientAs("Staff").DeleteAsync($"{Url}/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
