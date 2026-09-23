using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SEF_Project.Api.Data;
using SEF_Project.Api.DTOs.AgenticAI;
using SEF_Project.Api.Models.AgenticAI;
using SEF_Project.Api.Models.Enums;

namespace SEF_Project.Api.Services.AgenticAI;

public class InventoryAnalysisAgentService : IInventoryAnalysisAgentService
{
    private const int MaxModelAttempts = 2;
    private static readonly TimeSpan ModelTimeout = TimeSpan.FromSeconds(10);

    private readonly AppDbContext _context;
    private readonly IInventoryAgentToolRegistry _toolRegistry;
    private readonly IInventoryAnalysisModelClient _modelClient;
    private readonly ILogger<InventoryAnalysisAgentService> _logger;

    public InventoryAnalysisAgentService(
        AppDbContext context,
        IInventoryAgentToolRegistry toolRegistry,
        IInventoryAnalysisModelClient modelClient,
        ILogger<InventoryAnalysisAgentService> logger)
    {
        _context = context;
        _toolRegistry = toolRegistry;
        _modelClient = modelClient;
        _logger = logger;
    }

    public async Task<InventoryAnalysisResponseDto> AnalyzeAsync(
        InventoryAnalysisRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(request);

        var now = DateTime.UtcNow;
        var workflow = await ResolveWorkflowAsync(request, now, cancellationToken);
        var step = new AgentWorkflowStep
        {
            Workflow = workflow,
            StepOrder = workflow.Steps.Count + 1,
            AgentName = InventoryAgentConstants.AgentName,
            Title = "Analyze inventory and recommend actions",
            Status = AgentStepStatus.Running,
            StartedAt = now
        };

        workflow.Status = AgentWorkflowStatus.InProgress;
        workflow.StartedAt ??= now;
        workflow.PlanSummary =
            "Inventory Analysis Agent will inspect product inventory with allow-listed read-only tools and return structured recommendations.";
        _context.AgentWorkflowSteps.Add(step);

        await _context.SaveChangesAsync(cancellationToken);

        try
        {
            var toolResults = await ExecuteToolsAsync(
                step,
                request,
                cancellationToken);

            var output = await GenerateAndValidateOutputAsync(
                workflow,
                step,
                request,
                toolResults,
                cancellationToken);

            step.Status = AgentStepStatus.Completed;
            step.CompletedAt = DateTime.UtcNow;
            step.Summary =
                $"Produced {output.Recommendations.Count} inventory recommendation(s).";

            workflow.Status = AgentWorkflowStatus.Completed;
            workflow.CompletedAt = step.CompletedAt;
            workflow.FinalOutcome =
                $"Inventory Analysis Agent completed with {output.Recommendations.Count} structured recommendation(s).";

            await _context.SaveChangesAsync(cancellationToken);

            return new InventoryAnalysisResponseDto
            {
                WorkflowId = workflow.Id,
                StepId = step.Id,
                AgentName = step.AgentName,
                Status = workflow.Status.ToString(),
                Summary = step.Summary,
                Recommendations = output.Recommendations,
                ToolExecutions = step.ToolExecutions
                    .Select(t => new InventoryAgentToolExecutionDto
                    {
                        ToolName = t.ToolName,
                        Status = t.Status.ToString()
                    })
                    .ToList()
            };
        }
        catch (Exception exception)
        {
            await MarkFailedAsync(
                workflow,
                step,
                exception,
                cancellationToken);

            throw;
        }
    }

    private async Task<AgentWorkflow> ResolveWorkflowAsync(
        InventoryAnalysisRequestDto request,
        DateTime now,
        CancellationToken cancellationToken)
    {
        if (request.WorkflowId is null)
        {
            var workflow = new AgentWorkflow
            {
                Objective = request.Objective.Trim(),
                Status = AgentWorkflowStatus.Pending,
                StartedAt = now
            };

            _context.AgentWorkflows.Add(workflow);
            return workflow;
        }

        var existing = await _context.AgentWorkflows
            .Include(w => w.Steps)
            .FirstOrDefaultAsync(
                w => w.Id == request.WorkflowId.Value,
                cancellationToken);

        if (existing is null)
        {
            throw new ArgumentException("Agent workflow was not found.");
        }

        return existing;
    }

    private async Task<Dictionary<string, JsonDocument>> ExecuteToolsAsync(
        AgentWorkflowStep step,
        InventoryAnalysisRequestDto request,
        CancellationToken cancellationToken)
    {
        var toolResults = new Dictionary<string, JsonDocument>(StringComparer.Ordinal);

        var inventoryArguments = JsonDocument.Parse(JsonSerializer.Serialize(new
        {
            productIds = request.ProductIds,
            variantIds = request.VariantIds
        }));

        await ExecuteToolAsync(
            step,
            "GetProductInventory",
            inventoryArguments,
            toolResults,
            cancellationToken);

        var inventory = toolResults["GetProductInventory"];
        var hasInventory = inventory.RootElement.TryGetProperty(
                "inventory",
                out var inventoryItems)
            && inventoryItems.ValueKind == JsonValueKind.Array
            && inventoryItems.GetArrayLength() > 0;

        if (!hasInventory)
        {
            throw new InvalidOperationException(
                "No inventory data was available for the requested scope.");
        }

        await ExecuteToolAsync(
            step,
            "GetLowStockProducts",
            JsonDocument.Parse("{}"),
            toolResults,
            cancellationToken);

        if (request.VariantIds.Count > 0)
        {
            await ExecuteToolAsync(
                step,
                "GetStockHistory",
                JsonDocument.Parse(JsonSerializer.Serialize(new
                {
                    variantIds = request.VariantIds
                })),
                toolResults,
                cancellationToken);
        }

        if (request.ProductIds.Count > 0 || request.VariantIds.Count > 0)
        {
            await ExecuteToolAsync(
                step,
                "GetProductDetails",
                JsonDocument.Parse(JsonSerializer.Serialize(new
                {
                    productIds = request.ProductIds,
                    variantIds = request.VariantIds
                })),
                toolResults,
                cancellationToken);
        }

        return toolResults;
    }

    private async Task ExecuteToolAsync(
        AgentWorkflowStep step,
        string toolName,
        JsonDocument arguments,
        Dictionary<string, JsonDocument> toolResults,
        CancellationToken cancellationToken)
    {
        var execution = new AgentToolExecution
        {
            Step = step,
            ToolName = toolName,
            ToolArgumentsJson = arguments.RootElement.GetRawText(),
            Status = AgentToolStatus.Running,
            StartedAt = DateTime.UtcNow
        };

        step.ToolExecutions.Add(execution);
        _context.AgentToolExecutions.Add(execution);
        await _context.SaveChangesAsync(cancellationToken);

        try
        {
            var result = await _toolRegistry.ExecuteAsync(
                toolName,
                arguments,
                cancellationToken);

            execution.Status = AgentToolStatus.Success;
            execution.ToolResultJson = result.RootElement.GetRawText();
            execution.CompletedAt = DateTime.UtcNow;
            toolResults[toolName] = result;

            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            execution.Status = AgentToolStatus.Failed;
            execution.ErrorMessage = exception.Message;
            execution.CompletedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync(cancellationToken);

            throw;
        }
    }

    private async Task<InventoryAgentOutputDto> GenerateAndValidateOutputAsync(
        AgentWorkflow workflow,
        AgentWorkflowStep step,
        InventoryAnalysisRequestDto request,
        IReadOnlyDictionary<string, JsonDocument> toolResults,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= MaxModelAttempts; attempt++)
        {
            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken);
                timeout.CancelAfter(ModelTimeout);

                var rawOutput = await _modelClient.GenerateRecommendationsAsync(
                    request,
                    toolResults,
                    timeout.Token);

                var parsed = JsonSerializer.Deserialize<InventoryAgentOutputDto>(
                    rawOutput,
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                var validationErrors =
                    InventoryAgentOutputValidator.Validate(parsed);

                _context.AgentValidationResults.Add(new AgentValidationResult
                {
                    Step = step,
                    ValidatorName = "InventoryRecommendationSchema",
                    IsValid = validationErrors.Count == 0,
                    Severity = validationErrors.Count == 0
                        ? ValidationSeverity.Info
                        : ValidationSeverity.Error,
                    Message = validationErrors.Count == 0
                        ? "Inventory recommendation output matched schema."
                        : string.Join(" ", validationErrors)
                });

                await _context.SaveChangesAsync(cancellationToken);

                if (validationErrors.Count == 0 && parsed is not null)
                {
                    return parsed;
                }
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                await RecordWorkflowErrorAsync(
                    workflow,
                    step,
                    "Timeout",
                    "Inventory Analysis Agent model generation timed out.",
                    cancellationToken);
            }
            catch (JsonException exception)
            {
                await RecordWorkflowErrorAsync(
                    workflow,
                    step,
                    "InvalidModelOutput",
                    $"Inventory Analysis Agent returned malformed JSON: {exception.Message}",
                    cancellationToken);
            }

            _logger.LogWarning(
                "Inventory Analysis Agent output attempt {Attempt} failed validation for workflow {WorkflowId}.",
                attempt,
                workflow.Id);
        }

        throw new InvalidOperationException(
            "Inventory Analysis Agent could not produce valid structured output within retry limits.");
    }

    private async Task MarkFailedAsync(
        AgentWorkflow workflow,
        AgentWorkflowStep step,
        Exception exception,
        CancellationToken cancellationToken)
    {
        workflow.Status = AgentWorkflowStatus.Failed;
        workflow.CompletedAt = DateTime.UtcNow;
        workflow.FinalOutcome =
            "Inventory Analysis Agent failed before producing valid structured recommendations.";

        step.Status = AgentStepStatus.Failed;
        step.CompletedAt = workflow.CompletedAt;
        step.Summary = exception.Message;

        await RecordWorkflowErrorAsync(
            workflow,
            step,
            exception.GetType().Name,
            exception.Message,
            cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task RecordWorkflowErrorAsync(
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

    private static void ValidateRequest(InventoryAnalysisRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.Objective))
        {
            throw new ArgumentException("Objective is required.");
        }

        request.ProductIds = request.ProductIds
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();
        request.VariantIds = request.VariantIds
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();
    }
}
