using System.Net;
using System.Net.Http.Json;
using SEF_Project.Api.DTOs.Agents;
using SEF_Project.Api.Models.Enums;

namespace SEF_Project.Api.Tests;

public class InventoryPromotionAgentApiTests : IClassFixture<MarketingApiFactory>
{
    private const string Url = "/api/agents/inventory-promotion/workflows";

    private readonly MarketingApiFactory _factory;

    public InventoryPromotionAgentApiTests(MarketingApiFactory factory)
    {
        _factory = factory;
    }

    private static StartPromotionAgentRequest Request() => new()
    {
        Objective = "Find products with declining sales and recommend suitable promotions."
    };

    [Fact]
    public async Task StartWorkflow_ShouldRunAgentAndReturnCreated_WhenStaff()
    {
        var client = _factory.CreateClientAs("Staff");

        var response = await client.PostAsJsonAsync(Url, Request());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);

        var workflow = await response.Content.ReadFromJsonAsync<PromotionAgentWorkflowResponse>();

        // The seeded test database has no sales, so nothing qualifies.
        Assert.Equal(AgentWorkflowStatus.Completed, workflow!.Status);
        Assert.NotEmpty(workflow.Plan);
        Assert.Contains(workflow.ToolExecutions, t => t.ToolName == "GetSalesVelocity");

        var fetched = await client.GetFromJsonAsync<PromotionAgentWorkflowResponse>($"{Url}/{workflow.WorkflowId}");
        Assert.Equal(workflow.WorkflowId, fetched!.WorkflowId);

        var list = await client.GetFromJsonAsync<List<PromotionAgentWorkflowSummary>>(Url);
        Assert.Contains(list!, w => w.WorkflowId == workflow.WorkflowId);
    }

    [Fact]
    public async Task Decisions_ShouldReturnConflict_WhenWorkflowIsNotAwaitingApproval()
    {
        var client = _factory.CreateClientAs("Administrator");
        var created = await (await client.PostAsJsonAsync(Url, Request()))
            .Content.ReadFromJsonAsync<PromotionAgentWorkflowResponse>();

        var approve = await client.PostAsJsonAsync($"{Url}/{created!.WorkflowId}/approve", new ReviewPromotionAgentRequest());

        Assert.Equal(HttpStatusCode.Conflict, approve.StatusCode);
    }

    [Theory]
    [InlineData("Customer", HttpStatusCode.Forbidden)]
    [InlineData(null, HttpStatusCode.Unauthorized)]
    public async Task StartWorkflow_ShouldRejectNonStaff(string? role, HttpStatusCode expected)
    {
        var response = await _factory.CreateClientAs(role).PostAsJsonAsync(Url, Request());

        Assert.Equal(expected, response.StatusCode);
    }

    [Fact]
    public async Task StartWorkflow_ShouldReturnBadRequest_WhenInputContractIsViolated()
    {
        var response = await _factory.CreateClientAs("Staff").PostAsJsonAsync(Url, new
        {
            objective = "short",
            analysisDays = 500,
            maxDiscountPercent = 90
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Revise_ShouldReturnBadRequest_WithoutComment()
    {
        var response = await _factory.CreateClientAs("Staff")
            .PostAsJsonAsync($"{Url}/{Guid.NewGuid()}/revise", new { maxDiscountPercent = 10 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UnknownWorkflow_ShouldReturnNotFound()
    {
        var client = _factory.CreateClientAs("Staff");

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"{Url}/{Guid.NewGuid()}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.PostAsJsonAsync($"{Url}/{Guid.NewGuid()}/reject", new ReviewPromotionAgentRequest())).StatusCode);
    }
}
