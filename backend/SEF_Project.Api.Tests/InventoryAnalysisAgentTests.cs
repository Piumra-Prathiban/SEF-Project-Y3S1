using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SEF_Project.Api.Controllers;
using SEF_Project.Api.Data;
using SEF_Project.Api.DTOs.AgenticAI;
using SEF_Project.Api.DTOs.Catalog;
using SEF_Project.Api.Models;
using SEF_Project.Api.Models.AgenticAI;
using SEF_Project.Api.Models.Enums;
using SEF_Project.Api.Services.AgenticAI;
using SEF_Project.Api.Services.Catalog;

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
    public void InventoryAgentWorkflowsController_ShouldRequireStaffOrAdministrator()
    {
        var attribute = typeof(InventoryAgentWorkflowsController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .Single();

        Assert.Equal("Staff,Administrator", attribute.Roles);
    }

    [Fact]
    public async Task ListWorkflowsAsync_ShouldFilterAndPagePersistedWorkflows()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;

        context.AgentWorkflows.AddRange(
            new AgentWorkflow { Objective = "Review shirts", Status = AgentWorkflowStatus.AwaitingApproval, StartedAt = DateTime.UtcNow, Steps = new List<AgentWorkflowStep> { new() { AgentName = InventoryAgentConstants.AgentName, Title = "Analyze shirts" } } },
            new AgentWorkflow { Objective = "Completed jackets", Status = AgentWorkflowStatus.Completed, StartedAt = DateTime.UtcNow, Steps = new List<AgentWorkflowStep> { new() { AgentName = InventoryAgentConstants.AgentName, Title = "Analyze jackets" } } },
            new AgentWorkflow { Objective = "Other agent", Status = AgentWorkflowStatus.AwaitingApproval, StartedAt = DateTime.UtcNow, Steps = new List<AgentWorkflowStep> { new() { AgentName = "Marketing Agent", Title = "Analyze campaign" } } });
        await context.SaveChangesAsync();

        var service = CreateWorkflowService(context);
        var result = await service.ListWorkflowsAsync("AwaitingApproval", 1, 20);

        Assert.Equal(1, result.TotalItems);
        Assert.Equal("Review shirts", Assert.Single(result.Items).Objective);
        Assert.Equal("AwaitingApproval", result.Items[0].Status);
        var secondPage = await service.ListWorkflowsAsync(null, 2, 1);
        Assert.Equal(2, secondPage.TotalItems);
        Assert.Equal(2, secondPage.TotalPages);
        Assert.Single(secondPage.Items);
        Assert.Null(await service.GetWorkflowAsync(context.AgentWorkflows.Single(workflow => workflow.Objective == "Other agent").Id));
        await Assert.ThrowsAsync<ArgumentException>(() => service.ListWorkflowsAsync("Unknown", 1, 20));
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

        Assert.Equal("AwaitingApproval", response.Status);
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

        Assert.Equal(AgentWorkflowStatus.AwaitingApproval, workflow.Status);
        var step = Assert.Single(workflow.Steps);
        Assert.Equal(AgentStepStatus.Completed, step.Status);
        Assert.False(string.IsNullOrWhiteSpace(step.ResultJson));
        Assert.NotEmpty(step.ToolExecutions);
        Assert.Contains(step.ValidationResults, v => v.IsValid);
        Assert.DoesNotContain(
            step.ToolExecutions,
            t => t.ToolName == "AdjustStock");
    }

    [Fact]
    public async Task CreateWorkflowAsync_ShouldRequireApprovalBeforeStockOperation()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;

        var inventory = await context.Inventory.FirstAsync();
        inventory.QuantityOnHand = inventory.ReorderLevel;
        await context.SaveChangesAsync();

        var service = CreateWorkflowService(context);
        var staffUserId = await SeedUserAsync(context, "staff-approve@test.com", 2);

        var workflow = await service.CreateWorkflowAsync(new InventoryAnalysisRequestDto
        {
            Objective = "Restock low inventory."
        });

        var stockAfterWorkflow = await context.Inventory
            .AsNoTracking()
            .SingleAsync(i => i.Id == inventory.Id);
        var transactionsBeforeApproval = await context.InventoryTransactions
            .Where(t => t.Reference == $"AgentWorkflow:{workflow.WorkflowId}")
            .ToListAsync();

        Assert.Equal("AwaitingApproval", workflow.Status);
        Assert.Equal(inventory.ReorderLevel, stockAfterWorkflow.QuantityOnHand);
        Assert.Empty(transactionsBeforeApproval);
    }

    [Fact]
    public async Task ApproveAsync_ShouldExecuteApprovedStockOperationThroughInventoryService()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;

        var inventory = await context.Inventory.FirstAsync();
        inventory.QuantityOnHand = inventory.ReorderLevel;
        var beforeQuantity = inventory.QuantityOnHand;
        await context.SaveChangesAsync();

        var service = CreateWorkflowService(context);
        var staffUserId = await SeedUserAsync(context, "staff-approve@test.com", 2);

        var workflow = await service.CreateWorkflowAsync(new InventoryAnalysisRequestDto
        {
            Objective = "Restock low inventory."
        });

        var approved = await service.ApproveAsync(
            workflow.WorkflowId,
            reviewedByUserId: staffUserId,
            comment: "Approved for restock.");

        var inventoryAfterApproval = await context.Inventory
            .AsNoTracking()
            .SingleAsync(i => i.Id == inventory.Id);
        var transaction = await context.InventoryTransactions
            .AsNoTracking()
            .SingleAsync(t => t.Reference == $"AgentWorkflow:{workflow.WorkflowId}");

        Assert.NotNull(approved);
        Assert.Equal("Completed", approved.Status);
        Assert.True(inventoryAfterApproval.QuantityOnHand > beforeQuantity);
        Assert.Equal(InventoryTransactionType.StockIn, transaction.Type);
        Assert.True(transaction.QuantityChange > 0);
    }

    [Fact]
    public async Task RejectAsync_ShouldCompleteWithoutStockOperation()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;

        var inventory = await context.Inventory.FirstAsync();
        inventory.QuantityOnHand = inventory.ReorderLevel;
        await context.SaveChangesAsync();

        var service = CreateWorkflowService(context);
        var staffUserId = await SeedUserAsync(context, "staff-reject@test.com", 2);
        var workflow = await service.CreateWorkflowAsync(new InventoryAnalysisRequestDto
        {
            Objective = "Restock low inventory."
        });

        var rejected = await service.RejectAsync(
            workflow.WorkflowId,
            reviewedByUserId: staffUserId,
            comment: "Not needed.");

        var transactions = await context.InventoryTransactions
            .Where(t => t.Reference == $"AgentWorkflow:{workflow.WorkflowId}")
            .ToListAsync();

        Assert.NotNull(rejected);
        Assert.Equal("Cancelled", rejected.Status);
        Assert.Empty(transactions);
        Assert.Contains(rejected.Approvals, a => a.Status == "Rejected");
    }

    [Fact]
    public async Task RequestRevisionAsync_ShouldNotExecuteStockOperation()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;

        var service = CreateWorkflowService(context);
        var staffUserId = await SeedUserAsync(context, "staff-revise@test.com", 2);
        var workflow = await service.CreateWorkflowAsync(new InventoryAnalysisRequestDto
        {
            Objective = "Review inventory."
        });

        var revised = await service.RequestRevisionAsync(
            workflow.WorkflowId,
            reviewedByUserId: staffUserId,
            comment: "Clarify which supplier will be used.");

        Assert.NotNull(revised);
        Assert.Equal("Planning", revised.Status);
        Assert.Contains(revised.Approvals, a => a.Status == "RevisionRequested");
    }

    [Fact]
    public async Task ApproveAsync_ShouldFailSafely_WhenRecommendationInvalid()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;

        var service = CreateWorkflowService(context);
        var staffUserId = await SeedUserAsync(context, "staff-invalid@test.com", 2);
        var workflow = await service.CreateWorkflowAsync(new InventoryAnalysisRequestDto
        {
            Objective = "Review inventory."
        });

        var step = await context.AgentWorkflowSteps
            .SingleAsync(s => s.WorkflowId == workflow.WorkflowId);
        step.ResultJson =
            """{"recommendations":[{"variantId":"00000000-0000-0000-0000-000000000000","currentStock":1,"reorderLevel":2,"recommendedAction":"RESTOCK","recommendedQuantity":0,"reason":"bad"}]}""";
        await context.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ApproveAsync(
                workflow.WorkflowId,
                reviewedByUserId: staffUserId,
                comment: "Approve invalid recommendation."));

        var failed = await context.AgentWorkflows
            .Include(w => w.Errors)
            .SingleAsync(w => w.Id == workflow.WorkflowId);

        Assert.Equal(AgentWorkflowStatus.Failed, failed.Status);
        Assert.NotEmpty(failed.Errors);
    }

    [Fact]
    public async Task ApproveAsync_ShouldRejectUnauthorizedCustomerReviewer()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;

        var service = CreateWorkflowService(context);
        var customerUserId = await SeedUserAsync(
            context,
            "customer-reviewer@test.com",
            1);
        var workflow = await service.CreateWorkflowAsync(new InventoryAnalysisRequestDto
        {
            Objective = "Review inventory."
        });

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.ApproveAsync(
                workflow.WorkflowId,
                customerUserId,
                "Customer should not approve."));
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

    private static InventoryAgentWorkflowService CreateWorkflowService(
        AppDbContext context)
    {
        var agentService = new InventoryAnalysisAgentService(
            context,
            new InventoryAgentToolRegistry(context),
            new LocalInventoryAnalysisModelClient(),
            NullLogger<InventoryAnalysisAgentService>.Instance);

        return new InventoryAgentWorkflowService(
            context,
            agentService,
            new InventoryService(context));
    }

    private static async Task<int> SeedUserAsync(
        AppDbContext context,
        string email,
        int roleId)
    {
        var user = new User
        {
            Email = email,
            PasswordHash = "not-a-real-hash",
            FirstName = "Inventory",
            LastName = "Reviewer",
            RoleId = roleId,
            IsActive = true
        };

        context.Users.Add(user);
        await context.SaveChangesAsync();

        return user.Id;
    }
}
