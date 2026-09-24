using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SEF_Project.Api.Data;
using SEF_Project.Api.Models.AgenticAI;
using SEF_Project.Api.Models.Enums;

namespace SEF_Project.Api.Services.Recommendations;

public class AgentWorkflowRecorder : IAgentWorkflowRecorder
{
    private readonly AppDbContext _context;

    public AgentWorkflowRecorder(AppDbContext context)
    {
        _context = context;
    }

    public async Task<AgentWorkflowHandle> StartAsync(
        string objective,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(objective) || objective.Length > 2000)
        {
            throw new ArgumentException("A concise agent objective is required.");
        }

        var now = DateTime.UtcNow;
        var workflow = new AgentWorkflow
        {
            Id = Guid.NewGuid(),
            Objective = objective,
            Status = AgentWorkflowStatus.InProgress,
            PlanSummary =
                "Use only customer_preference, wishlist, product_search, and product_availability; validate grounded output.",
            StartedAt = now
        };
        var step = new AgentWorkflowStep
        {
            Id = Guid.NewGuid(),
            WorkflowId = workflow.Id,
            StepOrder = 1,
            AgentName = PersonalStylistAgentContract.AgentName,
            Title = "Generate grounded fashion recommendations",
            Status = AgentStepStatus.Running,
            Summary = "Structured tool orchestration; no hidden reasoning is stored.",
            StartedAt = now
        };

        workflow.Steps.Add(step);
        _context.AgentWorkflows.Add(workflow);
        await _context.SaveChangesAsync(cancellationToken);

        return new AgentWorkflowHandle(workflow.Id, step.Id);
    }

    public async Task<AgentToolExecutionHandle> StartToolAsync(
        AgentWorkflowHandle workflow,
        string toolName,
        string argumentsJson,
        CancellationToken cancellationToken = default)
    {
        if (!PersonalStylistToolNames.Allowed.Contains(toolName))
        {
            throw new InvalidOperationException(
                $"Tool '{toolName}' is not allowed for the Personal Stylist Agent.");
        }

        EnsureValidJson(argumentsJson);
        var execution = new AgentToolExecution
        {
            Id = Guid.NewGuid(),
            StepId = workflow.StepId,
            ToolName = toolName,
            ToolArgumentsJson = argumentsJson,
            Status = AgentToolStatus.Running,
            StartedAt = DateTime.UtcNow
        };

        _context.AgentToolExecutions.Add(execution);
        await _context.SaveChangesAsync(cancellationToken);
        return new AgentToolExecutionHandle(execution.Id);
    }

    public async Task CompleteToolAsync(
        AgentToolExecutionHandle execution,
        bool succeeded,
        string? resultJson,
        string? errorSummary,
        CancellationToken cancellationToken = default)
    {
        if (resultJson != null)
        {
            EnsureValidJson(resultJson);
        }

        var entity = await _context.AgentToolExecutions.SingleAsync(
            item => item.Id == execution.ToolExecutionId,
            cancellationToken);
        entity.Status = succeeded
            ? AgentToolStatus.Success
            : AgentToolStatus.Failed;
        entity.ToolResultJson = resultJson;
        entity.ErrorMessage = Limit(errorSummary, 2000);
        entity.CompletedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task RecordValidationAsync(
        AgentWorkflowHandle workflow,
        bool isValid,
        string message,
        CancellationToken cancellationToken = default)
    {
        _context.AgentValidationResults.Add(new AgentValidationResult
        {
            Id = Guid.NewGuid(),
            StepId = workflow.StepId,
            IsValid = isValid,
            ValidatorName = "CatalogueGroundingValidator",
            Message = Limit(message, 2000),
            Severity = isValid
                ? ValidationSeverity.Info
                : ValidationSeverity.Error
        });
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task CompleteAsync(
        AgentWorkflowHandle workflow,
        string finalResultJson,
        CancellationToken cancellationToken = default)
    {
        EnsureValidJson(finalResultJson);
        var now = DateTime.UtcNow;
        var entity = await _context.AgentWorkflows
            .Include(item => item.Steps)
            .SingleAsync(item => item.Id == workflow.WorkflowId, cancellationToken);
        var step = entity.Steps.Single(item => item.Id == workflow.StepId);

        entity.Status = AgentWorkflowStatus.Completed;
        entity.FinalOutcome = Limit(finalResultJson, 4000);
        entity.CompletedAt = now;
        step.Status = AgentStepStatus.Completed;
        step.Summary = "Returned only catalogue-grounded product variants.";
        step.CompletedAt = now;
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task FailAsync(
        AgentWorkflowHandle workflow,
        string errorType,
        string errorSummary,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var entity = await _context.AgentWorkflows
            .Include(item => item.Steps)
            .SingleAsync(item => item.Id == workflow.WorkflowId, cancellationToken);
        var step = entity.Steps.Single(item => item.Id == workflow.StepId);

        entity.Status = AgentWorkflowStatus.Failed;
        entity.FinalOutcome = "No recommendations returned.";
        entity.CompletedAt = now;
        step.Status = AgentStepStatus.Failed;
        step.Summary = "The agent failed safely without returning unverified products.";
        step.CompletedAt = now;
        _context.AgentWorkflowErrors.Add(new AgentWorkflowError
        {
            Id = Guid.NewGuid(),
            WorkflowId = workflow.WorkflowId,
            StepId = workflow.StepId,
            ErrorType = Limit(errorType, 100) ?? "AgentFailure",
            Message = Limit(errorSummary, 2000) ?? "The agent failed safely.",
            OccurredAt = now
        });
        await _context.SaveChangesAsync(cancellationToken);
    }

    private static void EnsureValidJson(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
        }
        catch (JsonException exception)
        {
            throw new ArgumentException(
                "Workflow summaries must contain valid JSON.",
                exception);
        }
    }

    private static string? Limit(string? value, int maximumLength) =>
        value == null || value.Length <= maximumLength
            ? value
            : value[..maximumLength];
}
