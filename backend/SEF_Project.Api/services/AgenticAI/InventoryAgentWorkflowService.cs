using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SEF_Project.Api.Data;
using SEF_Project.Api.DTOs.AgenticAI;
using SEF_Project.Api.DTOs.Catalog;
using SEF_Project.Api.DTOs.Common;
using SEF_Project.Api.Models.AgenticAI;
using SEF_Project.Api.Models.Enums;
using SEF_Project.Api.Services.Catalog;

namespace SEF_Project.Api.Services.AgenticAI;

public class InventoryAgentWorkflowService : IInventoryAgentWorkflowService
{
    private readonly AppDbContext _context;
    private readonly IInventoryAnalysisAgentService _agentService;
    private readonly IInventoryService _inventoryService;

    public InventoryAgentWorkflowService(
        AppDbContext context,
        IInventoryAnalysisAgentService agentService,
        IInventoryService inventoryService)
    {
        _context = context;
        _agentService = agentService;
        _inventoryService = inventoryService;
    }

    public async Task<InventoryAgentWorkflowResponseDto> CreateWorkflowAsync(
        InventoryAnalysisRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var analysis = await _agentService.AnalyzeAsync(request, cancellationToken);

        var workflow = await LoadWorkflowAsync(
            analysis.WorkflowId,
            cancellationToken);

        return MapWorkflow(workflow!);
    }

    public async Task<PagedResponse<InventoryAgentWorkflowSummaryDto>> ListWorkflowsAsync(
        string? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _context.AgentWorkflows
            .AsNoTracking()
            .Where(workflow => workflow.Steps.Any(step => step.AgentName == InventoryAgentConstants.AgentName));
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<AgentWorkflowStatus>(status, true, out var parsedStatus)
                || !Enum.IsDefined(parsedStatus))
            {
                throw new ArgumentException("Workflow status is not valid.");
            }

            query = query.Where(workflow => workflow.Status == parsedStatus);
        }

        var safePage = Math.Max(1, page);
        var safePageSize = Math.Clamp(pageSize, 1, 50);
        var totalItems = await query.CountAsync(cancellationToken);
        var workflows = await query
            .OrderByDescending(workflow => workflow.CreatedAt)
            .ThenByDescending(workflow => workflow.Id)
            .Skip((safePage - 1) * safePageSize)
            .Take(safePageSize)
            .Select(workflow => new InventoryAgentWorkflowSummaryDto
            {
                WorkflowId = workflow.Id,
                Objective = workflow.Objective,
                Status = workflow.Status.ToString(),
                StartedAt = workflow.StartedAt,
                CompletedAt = workflow.CompletedAt
            })
            .ToListAsync(cancellationToken);

        return new PagedResponse<InventoryAgentWorkflowSummaryDto>
        {
            Items = workflows,
            Page = safePage,
            PageSize = safePageSize,
            TotalItems = totalItems,
            TotalPages = Math.Max(1, (int)Math.Ceiling(totalItems / (double)safePageSize))
        };
    }

    public async Task<InventoryAgentWorkflowResponseDto?> GetWorkflowAsync(
        Guid workflowId,
        CancellationToken cancellationToken = default)
    {
        var workflow = await LoadWorkflowAsync(workflowId, cancellationToken);

        return workflow is null ? null : MapWorkflow(workflow);
    }

    public async Task<InventoryAgentWorkflowResponseDto?> ApproveAsync(
        Guid workflowId,
        int reviewedByUserId,
        string? comment,
        CancellationToken cancellationToken = default)
    {
        await EnsureAuthorizedReviewerAsync(reviewedByUserId, cancellationToken);

        var workflow = await LoadWorkflowAsync(workflowId, cancellationToken);

        if (workflow is null)
        {
            return null;
        }

        if (workflow.Status != AgentWorkflowStatus.AwaitingApproval)
        {
            throw new InvalidOperationException(
                "Only workflows awaiting approval can be approved.");
        }

        var step = GetAnalysisStep(workflow);
        InventoryAgentOutputDto output;

        try
        {
            output = ReadAgentOutput(step);
            await ValidateApprovedRecommendationsAsync(
                workflow,
                step,
                output,
                cancellationToken);
        }
        catch (Exception exception)
        {
            await MarkApprovalFailureAsync(
                workflow,
                step,
                exception,
                cancellationToken);
            throw;
        }

        var restockRecommendations = output.Recommendations
            .Where(r => r.RecommendedAction.Equals(
                    "RESTOCK",
                    StringComparison.OrdinalIgnoreCase)
                && r.RecommendedQuantity > 0)
            .ToList();

        var pendingApproval = workflow.Approvals
            .LastOrDefault(a => a.Status == ApprovalStatus.Pending);

        if (pendingApproval is not null)
        {
            pendingApproval.Status = ApprovalStatus.Approved;
            pendingApproval.ReviewedAt = DateTime.UtcNow;
            pendingApproval.ReviewedByUserId = reviewedByUserId;
            pendingApproval.Comment = comment;
        }
        else
        {
            workflow.Approvals.Add(new AgentApproval
            {
                Workflow = workflow,
                Step = step,
                Status = ApprovalStatus.Approved,
                RequestedAt = DateTime.UtcNow,
                ReviewedAt = DateTime.UtcNow,
                ReviewedByUserId = reviewedByUserId,
                Comment = comment
            });
        }

        await _context.SaveChangesAsync(cancellationToken);

        foreach (var recommendation in restockRecommendations)
        {
            try
            {
                await _inventoryService.AdjustStockAsync(
                    new StockAdjustmentDto
                    {
                        ProductVariantId = recommendation.VariantId,
                        Type = InventoryTransactionType.StockIn,
                        Quantity = recommendation.RecommendedQuantity,
                        Reference = $"AgentWorkflow:{workflow.Id}",
                        Reason =
                            $"Approved Inventory Analysis Agent recommendation: {recommendation.Reason}"
                    },
                    reviewedByUserId,
                    cancellationToken);
            }
            catch (Exception exception)
            {
                await MarkApprovalFailureAsync(
                    workflow,
                    step,
                    exception,
                    cancellationToken);
                throw;
            }
        }

        workflow.Status = AgentWorkflowStatus.Completed;
        workflow.CompletedAt = DateTime.UtcNow;
        workflow.FinalOutcome =
            restockRecommendations.Count == 0
                ? "Workflow approved. No high-impact stock operation was required."
                : $"Workflow approved and executed {restockRecommendations.Count} stock adjustment(s) through InventoryService.";

        await _context.SaveChangesAsync(cancellationToken);

        return MapWorkflow(workflow);
    }

    public async Task<InventoryAgentWorkflowResponseDto?> RejectAsync(
        Guid workflowId,
        int reviewedByUserId,
        string? comment,
        CancellationToken cancellationToken = default)
    {
        await EnsureAuthorizedReviewerAsync(reviewedByUserId, cancellationToken);

        var workflow = await LoadWorkflowAsync(workflowId, cancellationToken);

        if (workflow is null)
        {
            return null;
        }

        if (workflow.Status != AgentWorkflowStatus.AwaitingApproval)
        {
            throw new InvalidOperationException(
                "Only workflows awaiting approval can be rejected.");
        }

        var pendingApproval = workflow.Approvals
            .LastOrDefault(a => a.Status == ApprovalStatus.Pending);

        if (pendingApproval is not null)
        {
            pendingApproval.Status = ApprovalStatus.Rejected;
            pendingApproval.ReviewedAt = DateTime.UtcNow;
            pendingApproval.ReviewedByUserId = reviewedByUserId;
            pendingApproval.Comment = comment;
        }

        workflow.Status = AgentWorkflowStatus.Cancelled;
        workflow.CompletedAt = DateTime.UtcNow;
        workflow.FinalOutcome =
            "Inventory agent workflow was rejected by a human reviewer. No stock operation was executed.";

        await _context.SaveChangesAsync(cancellationToken);

        return MapWorkflow(workflow);
    }

    public async Task<InventoryAgentWorkflowResponseDto?> RequestRevisionAsync(
        Guid workflowId,
        int reviewedByUserId,
        string comment,
        CancellationToken cancellationToken = default)
    {
        await EnsureAuthorizedReviewerAsync(reviewedByUserId, cancellationToken);

        if (string.IsNullOrWhiteSpace(comment))
        {
            throw new ArgumentException("Revision comment is required.");
        }

        var workflow = await LoadWorkflowAsync(workflowId, cancellationToken);

        if (workflow is null)
        {
            return null;
        }

        if (workflow.Status != AgentWorkflowStatus.AwaitingApproval)
        {
            throw new InvalidOperationException(
                "Only workflows awaiting approval can be sent for revision.");
        }

        var pendingApproval = workflow.Approvals
            .LastOrDefault(a => a.Status == ApprovalStatus.Pending);

        if (pendingApproval is not null)
        {
            pendingApproval.Status = ApprovalStatus.RevisionRequested;
            pendingApproval.ReviewedAt = DateTime.UtcNow;
            pendingApproval.ReviewedByUserId = reviewedByUserId;
            pendingApproval.Comment = comment.Trim();
        }

        workflow.Status = AgentWorkflowStatus.Planning;
        workflow.FinalOutcome =
            "Human reviewer requested revision. No stock operation was executed.";

        await _context.SaveChangesAsync(cancellationToken);

        return MapWorkflow(workflow);
    }

    private async Task ValidateApprovedRecommendationsAsync(
        AgentWorkflow workflow,
        AgentWorkflowStep step,
        InventoryAgentOutputDto output,
        CancellationToken cancellationToken)
    {
        var schemaErrors = InventoryAgentOutputValidator.Validate(output);

        if (schemaErrors.Count > 0)
        {
            await RecordValidationAsync(
                step,
                false,
                string.Join(" ", schemaErrors),
                cancellationToken);
            await RecordErrorAsync(
                workflow,
                step,
                "InvalidRecommendation",
                "Approved recommendation failed deterministic schema validation.",
                cancellationToken);
            throw new InvalidOperationException(
                "Approved recommendation failed deterministic validation.");
        }

        foreach (var recommendation in output.Recommendations)
        {
            var inventory = await _context.Inventory
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    i => i.ProductVariantId == recommendation.VariantId,
                    cancellationToken);

            if (inventory is null)
            {
                await RecordErrorAsync(
                    workflow,
                    step,
                    "UnavailableInventory",
                    $"Inventory was unavailable for variant {recommendation.VariantId}.",
                    cancellationToken);
                throw new InvalidOperationException(
                    "Approved recommendation references unavailable inventory.");
            }

            if (recommendation.CurrentStock < 0
                || recommendation.ReorderLevel < 0
                || recommendation.RecommendedQuantity < 0)
            {
                throw new InvalidOperationException(
                    "Approved recommendation contains invalid negative values.");
            }

            if (recommendation.RecommendedAction.Equals(
                    "RESTOCK",
                    StringComparison.OrdinalIgnoreCase)
                && recommendation.RecommendedQuantity <= 0)
            {
                throw new InvalidOperationException(
                    "RESTOCK recommendations require a positive quantity.");
            }
        }

        await RecordValidationAsync(
            step,
            true,
            "Deterministic validation passed: reviewer authorization exists, variants exist, quantities are valid, output schema is valid and stock operations must go through InventoryService.",
            cancellationToken);
    }

    private async Task EnsureAuthorizedReviewerAsync(
        int reviewedByUserId,
        CancellationToken cancellationToken)
    {
        var reviewer = await _context.Users
            .AsNoTracking()
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Id == reviewedByUserId, cancellationToken);

        if (reviewer is null || !reviewer.IsActive)
        {
            throw new UnauthorizedAccessException(
                "Reviewer account is not active or was not found.");
        }

        if (reviewer.Role.Name is not ("Staff" or "Administrator"))
        {
            throw new UnauthorizedAccessException(
                "Only staff or administrators can review inventory agent workflows.");
        }
    }

    private static AgentWorkflowStep GetAnalysisStep(AgentWorkflow workflow) =>
        workflow.Steps
            .OrderByDescending(s => s.StepOrder)
            .FirstOrDefault(s => s.AgentName == InventoryAgentConstants.AgentName)
        ?? throw new InvalidOperationException(
            "Inventory Analysis Agent step was not found.");

    private static InventoryAgentOutputDto ReadAgentOutput(
        AgentWorkflowStep step)
    {
        if (string.IsNullOrWhiteSpace(step.ResultJson))
        {
            throw new InvalidOperationException(
                "Inventory Analysis Agent output is missing.");
        }

        return JsonSerializer.Deserialize<InventoryAgentOutputDto>(
                step.ResultJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException(
                "Inventory Analysis Agent output is malformed.");
    }

    private async Task<AgentWorkflow?> LoadWorkflowAsync(
        Guid workflowId,
        CancellationToken cancellationToken) =>
        await _context.AgentWorkflows
            .Include(w => w.Steps)
                .ThenInclude(s => s.ToolExecutions)
            .Include(w => w.Steps)
                .ThenInclude(s => s.ValidationResults)
            .Include(w => w.Approvals)
            .Include(w => w.Errors)
            .FirstOrDefaultAsync(w => w.Id == workflowId
                && w.Steps.Any(step => step.AgentName == InventoryAgentConstants.AgentName), cancellationToken);

    private async Task RecordValidationAsync(
        AgentWorkflowStep step,
        bool isValid,
        string message,
        CancellationToken cancellationToken)
    {
        _context.AgentValidationResults.Add(new AgentValidationResult
        {
            Step = step,
            ValidatorName = "DeterministicInventoryApprovalValidator",
            IsValid = isValid,
            Severity = isValid ? ValidationSeverity.Info : ValidationSeverity.Error,
            Message = message
        });

        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task RecordErrorAsync(
        AgentWorkflow workflow,
        AgentWorkflowStep step,
        string errorType,
        string message,
        CancellationToken cancellationToken)
    {
        _context.AgentWorkflowErrors.Add(new AgentWorkflowError
        {
            Workflow = workflow,
            Step = step,
            ErrorType = errorType,
            Message = message,
            OccurredAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task MarkApprovalFailureAsync(
        AgentWorkflow workflow,
        AgentWorkflowStep step,
        Exception exception,
        CancellationToken cancellationToken)
    {
        workflow.Status = AgentWorkflowStatus.Failed;
        workflow.CompletedAt = DateTime.UtcNow;
        workflow.FinalOutcome =
            "Workflow failed safely during deterministic approval validation or approved execution. No AI-direct database write was performed.";

        await RecordErrorAsync(
            workflow,
            step,
            exception.GetType().Name,
            exception.Message,
            cancellationToken);
    }

    private static InventoryAgentWorkflowResponseDto MapWorkflow(
        AgentWorkflow workflow) =>
        new()
        {
            WorkflowId = workflow.Id,
            Objective = workflow.Objective,
            Status = workflow.Status.ToString(),
            Plan = workflow.PlanSummary,
            FinalOutcome = workflow.FinalOutcome,
            StartedAt = workflow.StartedAt,
            CompletedAt = workflow.CompletedAt,
            Steps = workflow.Steps
                .OrderBy(s => s.StepOrder)
                .Select(MapStep)
                .ToList(),
            Approvals = workflow.Approvals
                .OrderBy(a => a.RequestedAt)
                .Select(a => new InventoryAgentApprovalDto
                {
                    Status = a.Status.ToString(),
                    RequestedAt = a.RequestedAt,
                    ReviewedAt = a.ReviewedAt,
                    ReviewedByUserId = a.ReviewedByUserId,
                    Comment = a.Comment
                })
                .ToList(),
            Errors = workflow.Errors
                .OrderBy(e => e.OccurredAt)
                .Select(e => $"{e.ErrorType}: {e.Message}")
                .ToList()
        };

    private static InventoryAgentWorkflowStepDto MapStep(
        AgentWorkflowStep step) =>
        new()
        {
            Id = step.Id,
            StepOrder = step.StepOrder,
            AgentName = step.AgentName,
            Title = step.Title,
            Status = step.Status.ToString(),
            Summary = step.Summary,
            Result = string.IsNullOrWhiteSpace(step.ResultJson)
                ? null
                : JsonSerializer.Deserialize<InventoryAgentOutputDto>(
                    step.ResultJson,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }),
            ToolExecutions = step.ToolExecutions
                .OrderBy(t => t.StartedAt)
                .Select(t => new InventoryAgentToolExecutionDto
                {
                    ToolName = t.ToolName,
                    Status = t.Status.ToString()
                })
                .ToList(),
            ValidationSummaries = step.ValidationResults
                .OrderBy(v => v.CreatedAt)
                .Select(v => $"{v.ValidatorName}: {v.Message}")
                .ToList()
        };
}
