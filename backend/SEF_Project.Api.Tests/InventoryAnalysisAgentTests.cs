using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SEF_Project.Api.Controllers;
using SEF_Project.Api.Data;
using SEF_Project.Api.DTOs.AgenticAI;
using SEF_Project.Api.Models.Enums;
using SEF_Project.Api.Services.AgenticAI;

namespace SEF_Project.Api.Tests;

public class InventoryAnalysisAgentTests
{
    private static async Task<(SqliteConnection Connection, AppDbContext Context)>
        CreateContextAsync()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();

        return (connection, context);
    }

    [Fact]
    public void InventoryAnalysisAgentController_ShouldRequireStaffOrAdministrator()
    {
        var attribute = typeof(InventoryAnalysisAgentController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .Single();

        Assert.Equal("Staff,Administrator", attribute.Roles);
    }

    [Fact]
    public async Task ToolRegistry_ShouldRejectUnauthorizedTools()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;

        var registry = new InventoryAgentToolRegistry(context);

        var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => registry.ExecuteAsync(
                "AdjustStock",
                JsonDocument.Parse("{}")));

        Assert.Contains("not allowed", exception.Message);
    }

    [Fact]
    public async Task ToolRegistry_ShouldValidateToolArguments()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;

        var registry = new InventoryAgentToolRegistry(context);

        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => registry.ExecuteAsync(
                "GetStockHistory",
                JsonDocument.Parse("""{"variantIds":["not-a-guid"]}""")));

        Assert.Contains("invalid GUID", exception.Message);
    }

    [Fact]
    public void OutputValidator_ShouldRejectMalformedStructuredOutput()
    {
        var output = new InventoryAgentOutputDto
        {
            Recommendations = new List<InventoryRecommendationDto>
            {
                new()
                {
                    VariantId = Guid.Empty,
                    CurrentStock = -1,
                    ReorderLevel = -1,
                    RecommendedAction = "DELETE_STOCK",
                    Reason = ""
                }
            }
        };

        var errors = InventoryAgentOutputValidator.Validate(output);

        Assert.Contains(errors, e => e.Contains("variantId"));
        Assert.Contains(errors, e => e.Contains("currentStock"));
        Assert.Contains(errors, e => e.Contains("reorderLevel"));
        Assert.Contains(errors, e => e.Contains("recommendedAction"));
        Assert.Contains(errors, e => e.Contains("reason"));
    }

    [Fact]
    public async Task AnalyzeAsync_ShouldPersistWorkflowStepToolAndValidationSummary()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;

        var service = new InventoryAnalysisAgentService(
            context,
            new InventoryAgentToolRegistry(context),
            new LocalInventoryAnalysisModelClient(),
            NullLogger<InventoryAnalysisAgentService>.Instance);

        var response = await service.AnalyzeAsync(new InventoryAnalysisRequestDto
        {
            Objective = "Identify inventory variants that need restocking."
        });

        Assert.Equal("Completed", response.Status);
        Assert.Equal(InventoryAgentConstants.AgentName, response.AgentName);
        Assert.NotEmpty(response.Recommendations);
        Assert.Contains(
            response.ToolExecutions,
            t => t.ToolName == "GetProductInventory"
                 && t.Status == AgentToolStatus.Success.ToString());

        var workflow = await context.AgentWorkflows
            .Include(w => w.Steps)
                .ThenInclude(s => s.ToolExecutions)
            .Include(w => w.Steps)
                .ThenInclude(s => s.ValidationResults)
            .SingleAsync(w => w.Id == response.WorkflowId);

        Assert.Equal(AgentWorkflowStatus.Completed, workflow.Status);
        var step = Assert.Single(workflow.Steps);
        Assert.Equal(AgentStepStatus.Completed, step.Status);
        Assert.NotEmpty(step.ToolExecutions);
        Assert.Contains(step.ValidationResults, v => v.IsValid);
        Assert.DoesNotContain(
            step.ToolExecutions,
            t => t.ToolName == "AdjustStock");
    }

    [Fact]
    public async Task AnalyzeAsync_ShouldFailWorkflow_WhenModelOutputInvalid()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;

        var service = new InventoryAnalysisAgentService(
            context,
            new InventoryAgentToolRegistry(context),
            new InvalidInventoryAnalysisModelClient(),
            NullLogger<InventoryAnalysisAgentService>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.AnalyzeAsync(new InventoryAnalysisRequestDto
            {
                Objective = "Analyze inventory."
            }));

        var workflow = await context.AgentWorkflows
            .Include(w => w.Errors)
            .Include(w => w.Steps)
                .ThenInclude(s => s.ValidationResults)
            .SingleAsync();

        Assert.Equal(AgentWorkflowStatus.Failed, workflow.Status);
        Assert.NotEmpty(workflow.Errors);
        Assert.Contains(
            workflow.Steps.SelectMany(s => s.ValidationResults),
            v => !v.IsValid);
    }

    private sealed class InvalidInventoryAnalysisModelClient
        : IInventoryAnalysisModelClient
    {
        public Task<string> GenerateRecommendationsAsync(
            InventoryAnalysisRequestDto request,
            IReadOnlyDictionary<string, JsonDocument> toolResults,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(
                """{"recommendations":[{"variantId":"00000000-0000-0000-0000-000000000000","currentStock":-1,"reorderLevel":-1,"recommendedAction":"DROP","reason":""}]}""");
    }
}
