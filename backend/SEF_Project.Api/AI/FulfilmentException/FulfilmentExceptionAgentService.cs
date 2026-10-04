using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SEF_Project.Api.Data;
using SEF_Project.Api.DTOs.Agents;
using SEF_Project.Api.DTOs.Orders;
using SEF_Project.Api.Models.AgenticAI;
using SEF_Project.Api.Models.Enums;
using SEF_Project.Api.Services.Orders;

namespace SEF_Project.Api.AI.FulfilmentException;

public interface IFulfilmentExceptionAgent
{
    Task<FulfilmentExceptionWorkflowResponse> StartAsync(
        StartFulfilmentExceptionRequest request,
        CancellationToken cancellationToken = default);

    Task<FulfilmentExceptionWorkflowResponse?> GetAsync(
        Guid workflowId,
        CancellationToken cancellationToken = default);

    Task<List<FulfilmentExceptionWorkflowSummary>> ListAsync(
        int limit,
        CancellationToken cancellationToken = default);

    Task<FulfilmentExceptionWorkflowResponse?> ApproveAsync(
        Guid workflowId,
        int reviewerUserId,
        string? comment,
        CancellationToken cancellationToken = default);

    Task<FulfilmentExceptionWorkflowResponse?> RejectAsync(
        Guid workflowId,
        int reviewerUserId,
        string? comment,
        CancellationToken cancellationToken = default);

    Task<FulfilmentExceptionWorkflowResponse?> ReviseAsync(
        Guid workflowId,
        int reviewerUserId,
        ReviseFulfilmentExceptionRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Orchestrates the Fulfilment Exception Agent: gather order facts (tools) →
/// draft a resolution (model) → validate (deterministic) → human approval →
/// re-validate → execute through <see cref="IOrderService"/>. Every stage is
/// persisted in the shared agent workflow tables; no reasoning text is stored.
/// </summary>
public sealed class FulfilmentExceptionAgentService : IFulfilmentExceptionAgent
{
    private static readonly string[] Plan =
    {
        "Retrieve order, payments, shipments and status history (GetOrderDetails, GetPayments, GetShipments, GetStatusHistory)",
        "Retrieve permitted transitions (GetPermittedTransitions)",
        "Draft a structured resolution (resolution model)",
        "Validate the resolution with deterministic business rules",
        "Pause for human approval (Staff or Administrator)",
        "Re-validate and execute the approved action through OrderService"
    };

    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    private readonly AppDbContext _context;
    private readonly FulfilmentExceptionToolRegistry _tools;
    private readonly IFulfilmentExceptionModel _model;
    private readonly IOrderService _orderService;
    private readonly FulfilmentExceptionAgentOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<FulfilmentExceptionAgentService> _logger;

    public FulfilmentExceptionAgentService(
        AppDbContext context,
        FulfilmentExceptionToolRegistry tools,
        IFulfilmentExceptionModel model,
        IOrderService orderService,
        IOptions<FulfilmentExceptionAgentOptions> options,
        TimeProvider timeProvider,
        ILogger<FulfilmentExceptionAgentService> logger)
    {
        _context = context;
        _tools = tools;
        _model = model;
        _orderService = orderService;
        _options = options.Value;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    // ---- Start ------------------------------------------------------------------

    public async Task<FulfilmentExceptionWorkflowResponse> StartAsync(
        StartFulfilmentExceptionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.OrderId == Guid.Empty)
        {
            throw new ArgumentException("A valid order id is required.");
        }

        var workflow = new AgentWorkflow
        {
            Objective = request.Objective.Trim(),
            Status = AgentWorkflowStatus.Planning,
            PlanSummary = SerializePlan(request.OrderId),
            StartedAt = Now()
        };

        _context.AgentWorkflows.Add(workflow);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "{AgentName} workflow {WorkflowId} started (model {Model}).",
            FulfilmentExceptionAgentConstants.AgentName, workflow.Id, _model.Name);

        await RunResolutionCycleAsync(workflow, request.OrderId, reviewerFeedback: null, cancellationToken);

        return (await GetAsync(workflow.Id, CancellationToken.None))!;
    }

    // ---- Resolution cycle (also used for revisions) -----------------------------

    private async Task RunResolutionCycleAsync(
        AgentWorkflow workflow,
        Guid orderId,
        string? reviewerFeedback,
        CancellationToken cancellationToken)
    {
        workflow.Status = AgentWorkflowStatus.InProgress;
        AgentWorkflowStep? step = null;

        try
        {
            step = await BeginStepAsync(workflow, FulfilmentExceptionAgentConstants.AgentName,
                "Gather order facts", cancellationToken);
            var data = await GatherAsync(step, orderId, cancellationToken);
            await CompleteStepAsync(step,
                "Retrieved the order, payments, shipments, status history and permitted transitions.",
                cancellationToken);

            step = await BeginStepAsync(workflow, FulfilmentExceptionAgentConstants.AgentName,
                "Draft a structured resolution", cancellationToken);
            var context = new FulfilmentExceptionContext(
                workflow.Objective,
                orderId,
                reviewerFeedback,
                data.Order,
                data.Payments,
                data.Shipments,
                data.History,
                data.PermittedTransitions);
            var document = await GenerateResolutionAsync(step, context, cancellationToken);
            await CompleteStepAsync(step, document.Summary, cancellationToken);

            step = await BeginStepAsync(workflow, FulfilmentExceptionAgentConstants.ValidatorName,
                "Validate resolution against business rules", cancellationToken);
            var valid = RecordValidation(
                step,
                FulfilmentExceptionValidator.Validate(document, data.PermittedTransitions));

            if (!valid)
            {
                await FailAsync(workflow, step, "ValidationFailed",
                    "The resolution failed deterministic validation; nothing was changed.");
                return;
            }

            await CompleteStepAsync(step, $"All {step.ValidationResults.Count} checks passed.", cancellationToken);

            if (string.Equals(document.Resolution.TargetType, "None", StringComparison.OrdinalIgnoreCase))
            {
                workflow.Status = AgentWorkflowStatus.Completed;
                workflow.CompletedAt = Now();
                workflow.FinalOutcome = "No permitted transition; nothing was changed.";
                await _context.SaveChangesAsync(cancellationToken);
                return;
            }

            var approvalStep = new AgentWorkflowStep
            {
                StepOrder = NextStepOrder(workflow),
                AgentName = FulfilmentExceptionAgentConstants.ReviewerName,
                Title = "Human approval required (Staff or Administrator)",
                Status = AgentStepStatus.Pending,
                StartedAt = Now(),
                Summary = $"Resolution for {document.Resolution.TargetType} awaiting review."
            };
            workflow.Steps.Add(approvalStep);
            workflow.Approvals.Add(new AgentApproval
            {
                Step = approvalStep,
                Status = ApprovalStatus.Pending,
                RequestedAt = Now()
            });
            workflow.Status = AgentWorkflowStatus.AwaitingApproval;
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Workflow {WorkflowId} awaiting approval to '{Action}' a {TargetType}.",
                workflow.Id, document.Resolution.Action, document.Resolution.TargetType);
        }
        catch (Exception ex)
        {
            await FailSafelyAsync(workflow, step, ex);
        }
    }

    private sealed record GatheredData(
        JsonNode Order,
        JsonNode Payments,
        JsonNode Shipments,
        JsonNode History,
        JsonNode PermittedTransitions);

    private async Task<GatheredData> GatherAsync(
        AgentWorkflowStep step,
        Guid orderId,
        CancellationToken cancellationToken)
    {
        JsonObject OrderArgs() => new() { ["orderId"] = orderId };

        var order = await _tools.ExecuteAsync(step,
            FulfilmentExceptionAgentConstants.GetOrderDetails, OrderArgs(), cancellationToken);
        var payments = await _tools.ExecuteAsync(step,
            FulfilmentExceptionAgentConstants.GetPayments, OrderArgs(), cancellationToken);
        var shipments = await _tools.ExecuteAsync(step,
            FulfilmentExceptionAgentConstants.GetShipments, OrderArgs(), cancellationToken);
        var history = await _tools.ExecuteAsync(step,
            FulfilmentExceptionAgentConstants.GetStatusHistory, OrderArgs(), cancellationToken);
        var transitions = await _tools.ExecuteAsync(step,
            FulfilmentExceptionAgentConstants.GetPermittedTransitions, OrderArgs(), cancellationToken);

        return new GatheredData(order, payments, shipments, history, transitions);
    }

    private async Task<FulfilmentExceptionDocument> GenerateResolutionAsync(
        AgentWorkflowStep step,
        FulfilmentExceptionContext context,
        CancellationToken cancellationToken)
    {
        var startedAt = Now();
        string raw;

        using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
        {
            timeout.CancelAfter(_options.ModelTimeout);

            try
            {
                raw = await _model.GenerateResolutionAsync(context, timeout.Token).WaitAsync(timeout.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                RecordSubmission(step, null, AgentToolStatus.Failed,
                    $"The resolution model did not respond within {_options.ModelTimeout.TotalSeconds:0.#} s.", startedAt);
                throw new ModelTimeoutException();
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                RecordSubmission(step, null, AgentToolStatus.Failed, "The resolution model failed.", startedAt);
                throw;
            }
        }

        try
        {
            var document = FulfilmentExceptionValidator.Parse(raw, _options.MaxModelOutputCharacters);
            RecordSubmission(step, document, AgentToolStatus.Success, null, startedAt);
            return document;
        }
        catch (ResolutionSchemaException ex)
        {
            RecordSubmission(step, null, AgentToolStatus.Failed,
                $"{ex.Message} (output of {raw?.Length ?? 0} characters was not stored).", startedAt);
            throw;
        }
    }

    // ---- Review decisions ------------------------------------------------------

    public async Task<FulfilmentExceptionWorkflowResponse?> ApproveAsync(
        Guid workflowId,
        int reviewerUserId,
        string? comment,
        CancellationToken cancellationToken = default)
    {
        var workflow = await LoadAsync(workflowId, cancellationToken);
        if (workflow is null)
        {
            return null;
        }

        var approval = PendingApproval(workflow, "approved");
        var document = LatestResolution(workflow)
            ?? throw new InvalidOperationException("There is no resolution to approve.");

        Decide(approval, ApprovalStatus.Approved, reviewerUserId, comment, $"Approved by user {reviewerUserId}.");
        workflow.Status = AgentWorkflowStatus.InProgress;
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Workflow {WorkflowId} approved by user {UserId}.", workflow.Id, reviewerUserId);

        AgentWorkflowStep? step = null;
        try
        {
            var orderId = ReadOrderId(workflow);

            // Data may have changed since the resolution: re-check before writing.
            step = await BeginStepAsync(workflow, FulfilmentExceptionAgentConstants.ValidatorName,
                "Re-validate against current data before execution", cancellationToken);
            var data = await GatherAsync(step, orderId, cancellationToken);
            var valid = RecordValidation(
                step,
                FulfilmentExceptionValidator.Validate(document, data.PermittedTransitions));

            if (!valid)
            {
                await FailAsync(workflow, step, "StaleResolution",
                    "The approved resolution no longer passes validation with current data; nothing was changed.");
                return await GetAsync(workflow.Id, CancellationToken.None);
            }

            await CompleteStepAsync(step, "Resolution is still valid.", cancellationToken);

            step = await BeginStepAsync(workflow, FulfilmentExceptionAgentConstants.ExecutorName,
                "Execute the approved action", cancellationToken);
            await ExecuteResolutionAsync(step, document, reviewerUserId, orderId, cancellationToken);
            await CompleteStepAsync(step, "Executed the approved action.", cancellationToken);

            workflow.Status = AgentWorkflowStatus.Completed;
            workflow.CompletedAt = Now();
            workflow.FinalOutcome = Truncate(
                $"Executed '{document.Resolution.Action}' on {document.Resolution.TargetType} "
                + $"'{document.Resolution.TargetId}' after approval by user {reviewerUserId}.", 4000);
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Workflow {WorkflowId} executed '{Action}'.", workflow.Id, document.Resolution.Action);
        }
        catch (Exception ex)
        {
            await FailSafelyAsync(workflow, step, ex);
        }

        return await GetAsync(workflow.Id, CancellationToken.None);
    }

    private async Task ExecuteResolutionAsync(
        AgentWorkflowStep step,
        FulfilmentExceptionDocument document,
        int reviewerUserId,
        Guid orderId,
        CancellationToken cancellationToken)
    {
        var resolution = document.Resolution;
        var startedAt = Now();

        switch (resolution.TargetType.ToLowerInvariant())
        {
            case "order" when string.Equals(resolution.Action, "Cancelled", StringComparison.OrdinalIgnoreCase):
                await _orderService.CancelOrderAsync(
                    reviewerUserId, canAccessAllOrders: true, orderId,
                    new CancelOrderRequest { Reason = resolution.Rationale }, cancellationToken);
                break;

            case "order":
                await _orderService.UpdateOrderStatusAsync(
                    reviewerUserId, canManageOrders: true, orderId,
                    new UpdateOrderStatusRequest
                    {
                        Status = Enum.Parse<OrderStatus>(resolution.Action, ignoreCase: true),
                        Note = resolution.Rationale
                    }, cancellationToken);
                break;

            case "payment":
                await _orderService.UpdatePaymentStatusAsync(
                    reviewerUserId, canManageOrders: true, orderId, resolution.TargetId!.Value,
                    new UpdatePaymentStatusRequest
                    {
                        Status = Enum.Parse<PaymentStatus>(resolution.Action, ignoreCase: true)
                    }, cancellationToken);
                break;

            case "shipment":
                await _orderService.UpdateShipmentStatusAsync(
                    reviewerUserId, canManageOrders: true, orderId, resolution.TargetId!.Value,
                    new UpdateShipmentRequest
                    {
                        Status = Enum.Parse<ShipmentStatus>(resolution.Action, ignoreCase: true)
                    }, cancellationToken);
                break;

            default:
                throw new InvalidOperationException("The resolution does not map to an executable action.");
        }

        step.ToolExecutions.Add(new AgentToolExecution
        {
            ToolName = FulfilmentExceptionAgentConstants.ApprovedAction,
            ToolArgumentsJson = JsonSerializer.Serialize(
                new { resolution.TargetType, resolution.TargetId, resolution.Action }, JsonOptions),
            ToolResultJson = JsonSerializer.Serialize(new { Executed = true }, JsonOptions),
            Status = AgentToolStatus.Success,
            StartedAt = startedAt,
            CompletedAt = Now()
        });
    }

    public async Task<FulfilmentExceptionWorkflowResponse?> RejectAsync(
        Guid workflowId,
        int reviewerUserId,
        string? comment,
        CancellationToken cancellationToken = default)
    {
        var workflow = await LoadAsync(workflowId, cancellationToken);
        if (workflow is null)
        {
            return null;
        }

        var approval = PendingApproval(workflow, "rejected");
        Decide(approval, ApprovalStatus.Rejected, reviewerUserId, comment, $"Rejected by user {reviewerUserId}.");

        workflow.Status = AgentWorkflowStatus.Cancelled;
        workflow.CompletedAt = Now();
        workflow.FinalOutcome = "Resolution rejected by the reviewer; nothing was changed.";
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Workflow {WorkflowId} rejected by user {UserId}.", workflow.Id, reviewerUserId);

        return await GetAsync(workflow.Id, cancellationToken);
    }

    public async Task<FulfilmentExceptionWorkflowResponse?> ReviseAsync(
        Guid workflowId,
        int reviewerUserId,
        ReviseFulfilmentExceptionRequest request,
        CancellationToken cancellationToken = default)
    {
        var workflow = await LoadAsync(workflowId, cancellationToken);
        if (workflow is null)
        {
            return null;
        }

        var approval = PendingApproval(workflow, "sent for revision");
        var revisions = workflow.Approvals.Count(a => a.Status == ApprovalStatus.RevisionRequested);
        if (revisions >= _options.MaxRevisions)
        {
            throw new InvalidOperationException(
                $"The revision limit ({_options.MaxRevisions}) has been reached. Approve or reject the resolution.");
        }

        var comment = request.Comment.Trim();
        Decide(approval, ApprovalStatus.RevisionRequested, reviewerUserId, comment,
            $"Revision requested by user {reviewerUserId}.");

        var orderId = ReadOrderId(workflow);
        workflow.Status = AgentWorkflowStatus.Planning;
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Workflow {WorkflowId} revision {Revision} requested by user {UserId}.",
            workflow.Id, revisions + 1, reviewerUserId);

        await RunResolutionCycleAsync(workflow, orderId, comment, cancellationToken);

        return await GetAsync(workflow.Id, CancellationToken.None);
    }

    // ---- Queries ----------------------------------------------------------------

    public async Task<FulfilmentExceptionWorkflowResponse?> GetAsync(
        Guid workflowId,
        CancellationToken cancellationToken = default)
    {
        var workflow = await LoadAsync(workflowId, cancellationToken, tracking: false);
        return workflow is null ? null : ToResponse(workflow);
    }

    public async Task<List<FulfilmentExceptionWorkflowSummary>> ListAsync(
        int limit,
        CancellationToken cancellationToken = default)
    {
        return await _context.AgentWorkflows
            .AsNoTracking()
            .Where(w => w.Steps.Any(s => s.AgentName == FulfilmentExceptionAgentConstants.AgentName))
            .OrderByDescending(w => w.CreatedAt)
            .Take(Math.Clamp(limit, 1, 100))
            .Select(w => new FulfilmentExceptionWorkflowSummary
            {
                WorkflowId = w.Id,
                Objective = w.Objective,
                Status = w.Status,
                CreatedAt = w.CreatedAt,
                CompletedAt = w.CompletedAt
            })
            .ToListAsync(cancellationToken);
    }

    // ---- Persistence helpers ------------------------------------------------------

    private async Task<AgentWorkflow?> LoadAsync(
        Guid workflowId,
        CancellationToken cancellationToken,
        bool tracking = true)
    {
        IQueryable<AgentWorkflow> query = _context.AgentWorkflows
            .Include(w => w.Steps).ThenInclude(s => s.ToolExecutions)
            .Include(w => w.Steps).ThenInclude(s => s.ValidationResults)
            .Include(w => w.Approvals)
            .Include(w => w.Errors)
            .AsSplitQuery();

        if (!tracking)
        {
            query = query.AsNoTracking();
        }

        var workflow = await query.FirstOrDefaultAsync(w => w.Id == workflowId, cancellationToken);

        return workflow is not null && workflow.Steps.Any(s => s.AgentName == FulfilmentExceptionAgentConstants.AgentName)
            ? workflow
            : null;
    }

    private async Task<AgentWorkflowStep> BeginStepAsync(
        AgentWorkflow workflow,
        string agentName,
        string title,
        CancellationToken cancellationToken)
    {
        var step = new AgentWorkflowStep
        {
            StepOrder = NextStepOrder(workflow),
            AgentName = agentName,
            Title = title,
            Status = AgentStepStatus.Running,
            StartedAt = Now()
        };

        workflow.Steps.Add(step);
        await _context.SaveChangesAsync(cancellationToken);
        return step;
    }

    private async Task CompleteStepAsync(AgentWorkflowStep step, string summary, CancellationToken cancellationToken)
    {
        step.Status = AgentStepStatus.Completed;
        step.CompletedAt = Now();
        step.Summary = Truncate(summary, 4000);
        await _context.SaveChangesAsync(cancellationToken);
    }

    private static int NextStepOrder(AgentWorkflow workflow) =>
        workflow.Steps.Count == 0 ? 1 : workflow.Steps.Max(s => s.StepOrder) + 1;

    private static bool RecordValidation(AgentWorkflowStep step, IEnumerable<ValidationOutcome> outcomes)
    {
        var valid = true;

        foreach (var outcome in outcomes)
        {
            valid &= outcome.IsValid;
            step.ValidationResults.Add(new AgentValidationResult
            {
                ValidatorName = outcome.Rule,
                IsValid = outcome.IsValid,
                Severity = outcome.IsValid ? ValidationSeverity.Info : ValidationSeverity.Error,
                Message = Truncate(outcome.Message, 2000)
            });
        }

        return valid;
    }

    private void RecordSubmission(
        AgentWorkflowStep step,
        FulfilmentExceptionDocument? document,
        AgentToolStatus status,
        string? error,
        DateTime startedAt)
    {
        step.ToolExecutions.Add(new AgentToolExecution
        {
            ToolName = FulfilmentExceptionAgentConstants.SubmitResolution,
            ToolArgumentsJson = document is null ? null : JsonSerializer.Serialize(document, JsonOptions),
            ToolResultJson = JsonSerializer.Serialize(new { Model = _model.Name }, JsonOptions),
            Status = status,
            ErrorMessage = error is null ? null : Truncate(error, 2000),
            StartedAt = startedAt,
            CompletedAt = Now()
        });
    }

    private void Decide(AgentApproval approval, ApprovalStatus status, int reviewerUserId, string? comment, string stepSummary)
    {
        approval.Status = status;
        approval.ReviewedByUserId = reviewerUserId;
        approval.ReviewedAt = Now();
        approval.Comment = string.IsNullOrWhiteSpace(comment) ? null : Truncate(comment.Trim(), 2000);

        if (approval.Step is not null)
        {
            approval.Step.Status = AgentStepStatus.Completed;
            approval.Step.CompletedAt = Now();
            approval.Step.Summary = stepSummary;
        }
    }

    private static AgentApproval PendingApproval(AgentWorkflow workflow, string action)
    {
        var pending = workflow.Approvals.FirstOrDefault(a => a.Status == ApprovalStatus.Pending);

        if (workflow.Status != AgentWorkflowStatus.AwaitingApproval || pending is null)
        {
            throw new InvalidOperationException(
                $"Only workflows awaiting approval can be {action} (current status: {workflow.Status}).");
        }

        return pending;
    }

    private async Task FailAsync(AgentWorkflow workflow, AgentWorkflowStep? step, string errorType, string message)
    {
        var now = Now();

        if (step is not null && step.Status is AgentStepStatus.Running or AgentStepStatus.Pending)
        {
            step.Status = AgentStepStatus.Failed;
            step.CompletedAt = now;
            step.Summary ??= message;
        }

        workflow.Errors.Add(new AgentWorkflowError
        {
            Step = step,
            ErrorType = errorType,
            Message = Truncate(message, 2000),
            OccurredAt = now
        });
        workflow.Status = AgentWorkflowStatus.Failed;
        workflow.CompletedAt = now;
        workflow.FinalOutcome = $"Failed safely ({errorType}); no order state was changed.";

        await _context.SaveChangesAsync(CancellationToken.None);

        _logger.LogWarning("Workflow {WorkflowId} failed: {ErrorType}.", workflow.Id, errorType);
    }

    private async Task FailSafelyAsync(AgentWorkflow workflow, AgentWorkflowStep? step, Exception ex)
    {
        var (type, message) = ex switch
        {
            ToolPermissionException p => ("ToolNotPermitted", p.Message),
            FulfilmentExceptionToolException { IsTimeout: true } t => ("ToolTimeout", t.Message),
            FulfilmentExceptionToolException t => ("ToolFailure", t.Message),
            ResolutionSchemaException s => ("MalformedOutput", $"The resolution was rejected: {s.Message}"),
            ModelTimeoutException => ("ModelTimeout", "The resolution model did not respond in time."),
            _ => ("UnexpectedError", "The agent stopped because of an unexpected error. Nothing was changed.")
        };

        _logger.LogError(ex, "Workflow {WorkflowId} stopped with {ErrorType}.", workflow.Id, type);
        await FailAsync(workflow, step, type, message);
    }

    // ---- Stored data readers ------------------------------------------------------

    private sealed record PlanDocument(string Agent, List<string> Plan, Guid OrderId);

    private static string SerializePlan(Guid orderId) =>
        JsonSerializer.Serialize(
            new PlanDocument(FulfilmentExceptionAgentConstants.AgentName, Plan.ToList(), orderId),
            JsonOptions);

    private static PlanDocument? ReadPlan(AgentWorkflow workflow)
    {
        try
        {
            return string.IsNullOrWhiteSpace(workflow.PlanSummary)
                ? null
                : JsonSerializer.Deserialize<PlanDocument>(workflow.PlanSummary, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static Guid ReadOrderId(AgentWorkflow workflow) =>
        ReadPlan(workflow)?.OrderId
        ?? throw new InvalidOperationException("The workflow order id could not be read.");

    private static IEnumerable<(AgentWorkflowStep Step, AgentToolExecution Execution)> Executions(
        AgentWorkflow workflow,
        string toolName) =>
        workflow.Steps
            .OrderBy(s => s.StepOrder)
            .SelectMany(s => s.ToolExecutions
                .Where(t => t.ToolName == toolName && t.Status == AgentToolStatus.Success)
                .OrderBy(t => t.StartedAt)
                .ThenBy(t => t.CreatedAt)
                .Select(t => (s, t)));

    private static FulfilmentExceptionDocument? LatestResolution(AgentWorkflow workflow)
    {
        var latest = Executions(workflow, FulfilmentExceptionAgentConstants.SubmitResolution)
            .LastOrDefault().Execution;

        return latest?.ToolArgumentsJson is null
            ? null
            : JsonSerializer.Deserialize<FulfilmentExceptionDocument>(latest.ToolArgumentsJson, JsonOptions);
    }

    private FulfilmentExceptionWorkflowResponse ToResponse(AgentWorkflow workflow)
    {
        var steps = workflow.Steps.OrderBy(s => s.StepOrder).ToList();

        return new FulfilmentExceptionWorkflowResponse
        {
            WorkflowId = workflow.Id,
            Objective = workflow.Objective,
            Status = workflow.Status,
            Plan = ReadPlan(workflow)?.Plan ?? new List<string>(),
            Resolution = LatestResolution(workflow),
            Steps = steps.Select(s => new WorkflowStepResponse
            {
                StepOrder = s.StepOrder,
                AgentName = s.AgentName,
                Title = s.Title,
                Status = s.Status,
                Summary = s.Summary
            }).ToList(),
            ToolExecutions = steps.SelectMany(s => s.ToolExecutions
                .OrderBy(t => t.StartedAt)
                .ThenBy(t => t.CreatedAt)
                .Select(t => new ToolExecutionResponse
                {
                    StepOrder = s.StepOrder,
                    ToolName = t.ToolName,
                    Status = t.Status,
                    Arguments = ParseJson(t.ToolArgumentsJson),
                    Result = ParseJson(t.ToolResultJson),
                    ErrorMessage = t.ErrorMessage,
                    StartedAt = t.StartedAt,
                    CompletedAt = t.CompletedAt
                })).ToList(),
            ValidationResults = steps.SelectMany(s => s.ValidationResults.Select(v => new ValidationResultResponse
            {
                StepOrder = s.StepOrder,
                ValidatorName = v.ValidatorName,
                IsValid = v.IsValid,
                Severity = v.Severity,
                Message = v.Message
            })).ToList(),
            Approvals = workflow.Approvals
                .OrderBy(a => a.RequestedAt)
                .ThenBy(a => a.Step?.StepOrder ?? 0)
                .Select(a => new ApprovalResponse
                {
                    Status = a.Status,
                    RequestedAt = a.RequestedAt,
                    ReviewedByUserId = a.ReviewedByUserId,
                    ReviewedAt = a.ReviewedAt,
                    Comment = a.Comment
                }).ToList(),
            Errors = workflow.Errors
                .OrderBy(e => e.OccurredAt)
                .Select(e => new WorkflowErrorResponse
                {
                    ErrorType = e.ErrorType,
                    Message = e.Message,
                    OccurredAt = e.OccurredAt
                }).ToList(),
            FinalOutcome = workflow.FinalOutcome,
            StartedAt = workflow.StartedAt,
            CompletedAt = workflow.CompletedAt
        };
    }

    private static JsonNode? ParseJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonNode.Parse(json);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max];

    private DateTime Now() => _timeProvider.GetUtcNow().UtcDateTime;

    private sealed class ModelTimeoutException : Exception
    {
    }
}
