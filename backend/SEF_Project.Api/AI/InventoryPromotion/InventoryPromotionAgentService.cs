using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SEF_Project.Api.Data;
using SEF_Project.Api.DTOs.Agents;
using SEF_Project.Api.DTOs.Marketing;
using SEF_Project.Api.Models.AgenticAI;
using SEF_Project.Api.Models.Enums;
using SEF_Project.Api.Services.Marketing;

namespace SEF_Project.Api.AI.InventoryPromotion;

public interface IInventoryPromotionAgent
{
    Task<PromotionAgentWorkflowResponse> StartAsync(
        StartPromotionAgentRequest request,
        CancellationToken cancellationToken = default);

    Task<PromotionAgentWorkflowResponse?> GetAsync(Guid workflowId, CancellationToken cancellationToken = default);

    Task<List<PromotionAgentWorkflowSummary>> ListAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>High-impact proposals require <paramref name="reviewerIsAdministrator"/>.</summary>
    Task<PromotionAgentWorkflowResponse?> ApproveAsync(
        Guid workflowId,
        int reviewerUserId,
        bool reviewerIsAdministrator,
        string? comment,
        CancellationToken cancellationToken = default);

    Task<PromotionAgentWorkflowResponse?> RejectAsync(
        Guid workflowId,
        int reviewerUserId,
        string? comment,
        CancellationToken cancellationToken = default);

    Task<PromotionAgentWorkflowResponse?> ReviseAsync(
        Guid workflowId,
        int reviewerUserId,
        RevisePromotionAgentRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Orchestrates the Inventory &amp; Promotion Agent:
/// gather data (tools) → draft proposal (model) → price (tool) → validate
/// (deterministic) → human approval → re-validate → execute through
/// <see cref="IPromotionService"/>. Every stage is persisted in the shared
/// agent workflow tables. No reasoning text is stored.
/// </summary>
public sealed class InventoryPromotionAgentService : IInventoryPromotionAgent
{
    private const int MaxCandidateProducts = 50;

    private static readonly string[] Plan =
    {
        "Retrieve sales velocity (GetSalesVelocity)",
        "Retrieve live promotions (GetActivePromotions)",
        "Retrieve inventory, product details and pricing for candidate products (GetInventory, GetProductDetails, GetProductPricing)",
        "Draft a structured promotion proposal (proposal model)",
        "Price each proposal on the server (CalculatePromotion)",
        "Validate the proposal with deterministic business rules",
        "Pause for human approval (Administrator for high-impact proposals)",
        "Re-validate and create approved promotions through PromotionService"
    };

    private readonly AppDbContext _context;
    private readonly PromotionAgentToolRegistry _tools;
    private readonly IPromotionProposalModel _model;
    private readonly IPromotionService _promotionService;
    private readonly InventoryPromotionAgentOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<InventoryPromotionAgentService> _logger;

    public InventoryPromotionAgentService(
        AppDbContext context,
        PromotionAgentToolRegistry tools,
        IPromotionProposalModel model,
        IPromotionService promotionService,
        IOptions<InventoryPromotionAgentOptions> options,
        TimeProvider timeProvider,
        ILogger<InventoryPromotionAgentService> logger)
    {
        _context = context;
        _tools = tools;
        _model = model;
        _promotionService = promotionService;
        _options = options.Value;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    // ---- Start ------------------------------------------------------------------

    public async Task<PromotionAgentWorkflowResponse> StartAsync(
        StartPromotionAgentRequest request,
        CancellationToken cancellationToken = default)
    {
        var input = new AgentInput(
            request.Focus,
            request.AnalysisDays,
            request.MaxProposals,
            request.MaxDiscountPercent,
            new List<Guid>());

        var workflow = new AgentWorkflow
        {
            Objective = request.Objective.Trim(),
            Status = AgentWorkflowStatus.Planning,
            PlanSummary = SerializePlan(input),
            StartedAt = Now()
        };

        _context.AgentWorkflows.Add(workflow);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "{AgentName} workflow {WorkflowId} started (focus {Focus}, model {Model}).",
            PromotionAgentConstants.AgentName, workflow.Id, input.Focus, _model.Name);

        await RunProposalCycleAsync(workflow, input, reviewerFeedback: null, cancellationToken);

        return (await GetAsync(workflow.Id, CancellationToken.None))!;
    }

    // ---- Proposal cycle (also used for revisions) -------------------------------

    private async Task RunProposalCycleAsync(
        AgentWorkflow workflow,
        AgentInput input,
        string? reviewerFeedback,
        CancellationToken cancellationToken)
    {
        workflow.Status = AgentWorkflowStatus.InProgress;
        AgentWorkflowStep? step = null;

        try
        {
            step = await BeginStepAsync(workflow, PromotionAgentConstants.AgentName,
                "Gather sales, inventory, promotion and pricing data", cancellationToken);
            var data = await GatherAsync(step, input.AnalysisDays, productIds: null, cancellationToken);
            await CompleteStepAsync(step,
                $"Retrieved sales velocity for {Count(data.Velocity, "items")} variant(s), "
                + $"{Count(data.ActivePromotions, "items")} live promotion(s) and details for "
                + $"{Count(data.Details, "products")} product(s).", cancellationToken);

            step = await BeginStepAsync(workflow, PromotionAgentConstants.AgentName,
                "Draft structured promotion proposal", cancellationToken);
            var context = new PromotionAgentContext(
                workflow.Objective, input.Focus, input.AnalysisDays, input.MaxProposals, input.MaxDiscountPercent,
                Now(), input.ExcludeProductIds, reviewerFeedback,
                data.Velocity, data.Inventory, data.ActivePromotions, data.Details, data.Pricing);
            var document = await GenerateProposalAsync(step, context, cancellationToken);
            await CompleteStepAsync(step, document.Summary, cancellationToken);

            var serverPricing = new Dictionary<Guid, JsonNode?>();
            if (document.Proposals.Count > 0)
            {
                step = await BeginStepAsync(workflow, PromotionAgentConstants.AgentName,
                    "Price proposals on the server", cancellationToken);
                await PriceProposalsAsync(step, document, serverPricing, cancellationToken);
                await CompleteStepAsync(step, $"Priced {serverPricing.Count(p => p.Value is not null)} of "
                    + $"{document.Proposals.Count} proposal(s).", cancellationToken);
            }

            step = await BeginStepAsync(workflow, PromotionAgentConstants.ValidatorName,
                "Validate proposal against business rules", cancellationToken);
            var facts = PromotionAgentData.Build(data.Velocity, data.Inventory, data.ActivePromotions, data.Details, data.Pricing);
            var valid = RecordValidation(step, PromotionProposalValidator.Validate(
                document,
                new ProposalValidationInput(facts, serverPricing, input.MaxProposals, input.MaxDiscountPercent,
                    _options.MaxPromotionDays, Now(), input.ExcludeProductIds)));

            if (!valid)
            {
                await FailAsync(workflow, step, "ValidationFailed",
                    "The proposal failed deterministic validation; nothing was changed.");
                return;
            }

            await CompleteStepAsync(step, $"All {step.ValidationResults.Count} checks passed.", cancellationToken);

            if (document.Proposals.Count == 0)
            {
                workflow.Status = AgentWorkflowStatus.Completed;
                workflow.CompletedAt = Now();
                workflow.FinalOutcome = "No products needed a promotion; nothing was changed.";
                await _context.SaveChangesAsync(cancellationToken);
                return;
            }

            var impact = PromotionProposalValidator.AssessImpact(document, facts, _options);
            var approvalStep = new AgentWorkflowStep
            {
                StepOrder = NextStepOrder(workflow),
                AgentName = PromotionAgentConstants.ReviewerName,
                Title = impact == PromotionImpactLevel.High
                    ? "Human approval required (high impact: Administrator)"
                    : "Human approval required (low impact: Staff or Administrator)",
                Status = AgentStepStatus.Pending,
                StartedAt = Now(),
                Summary = $"{document.Proposals.Count} promotion(s) awaiting review."
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
                "Workflow {WorkflowId} awaiting {Impact}-impact approval for {Count} proposal(s).",
                workflow.Id, impact, document.Proposals.Count);
        }
        catch (Exception ex)
        {
            await FailSafelyAsync(workflow, step, ex);
        }
    }

    private sealed record GatheredData(
        JsonNode Velocity,
        JsonNode ActivePromotions,
        JsonNode Inventory,
        JsonNode Details,
        JsonNode Pricing);

    private async Task<GatheredData> GatherAsync(
        AgentWorkflowStep step,
        int analysisDays,
        IReadOnlyCollection<Guid>? productIds,
        CancellationToken cancellationToken)
    {
        var velocity = await _tools.ExecuteAsync(step, PromotionAgentConstants.GetSalesVelocity,
            new JsonObject { ["analysisDays"] = analysisDays, ["limit"] = 100 }, cancellationToken);
        var live = await _tools.ExecuteAsync(step, PromotionAgentConstants.GetActivePromotions,
            new JsonObject(), cancellationToken);

        var candidates = (productIds ?? velocity["items"]!.AsArray()
                .Select(item => item!["productId"]!.Deserialize<Guid>(AgentJson.Options))
                .ToList())
            .Distinct()
            .Take(MaxCandidateProducts)
            .ToList();

        if (candidates.Count == 0)
        {
            return new GatheredData(velocity, live,
                new JsonObject { ["items"] = new JsonArray() },
                new JsonObject { ["products"] = new JsonArray(), ["missingProductIds"] = new JsonArray() },
                new JsonObject { ["products"] = new JsonArray(), ["missingProductIds"] = new JsonArray() });
        }

        JsonObject ProductArgs() => new() { ["productIds"] = AgentJson.ToNode(candidates) };

        var inventory = await _tools.ExecuteAsync(step, PromotionAgentConstants.GetInventory, ProductArgs(), cancellationToken);
        var details = await _tools.ExecuteAsync(step, PromotionAgentConstants.GetProductDetails, ProductArgs(), cancellationToken);
        var pricing = await _tools.ExecuteAsync(step, PromotionAgentConstants.GetProductPricing, ProductArgs(), cancellationToken);

        return new GatheredData(velocity, live, inventory, details, pricing);
    }

    private async Task PriceProposalsAsync(
        AgentWorkflowStep step,
        PromotionProposalDocument document,
        Dictionary<Guid, JsonNode?> serverPricing,
        CancellationToken cancellationToken)
    {
        foreach (var proposal in document.Proposals)
        {
            try
            {
                serverPricing[proposal.ProductId] = await _tools.ExecuteAsync(step,
                    PromotionAgentConstants.CalculatePromotion,
                    new JsonObject
                    {
                        ["productId"] = proposal.ProductId,
                        ["promotionType"] = proposal.PromotionType,
                        ["discountValue"] = proposal.DiscountValue,
                        ["startDate"] = proposal.StartDate,
                        ["endDate"] = proposal.EndDate
                    },
                    cancellationToken);
            }
            catch (PromotionAgentToolException ex) when (ex.IsRejected)
            {
                // A rejected price (e.g. unknown product) is reported by validation.
                serverPricing[proposal.ProductId] = null;
            }
        }
    }

    private async Task<PromotionProposalDocument> GenerateProposalAsync(
        AgentWorkflowStep step,
        PromotionAgentContext context,
        CancellationToken cancellationToken)
    {
        var startedAt = Now();
        string raw;

        using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
        {
            timeout.CancelAfter(_options.ModelTimeout);

            try
            {
                raw = await _model.GenerateProposalAsync(context, timeout.Token).WaitAsync(timeout.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                RecordSubmission(step, null, AgentToolStatus.Failed,
                    $"The proposal model did not respond within {_options.ModelTimeout.TotalSeconds:0.#} s.", startedAt);
                throw new ModelTimeoutException();
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                RecordSubmission(step, null, AgentToolStatus.Failed, "The proposal model failed.", startedAt);
                throw;
            }
        }

        try
        {
            var document = PromotionProposalValidator.Parse(raw, _options.MaxModelOutputCharacters);
            RecordSubmission(step, document, AgentToolStatus.Success, null, startedAt);
            return document;
        }
        catch (ProposalSchemaException ex)
        {
            // The raw text is not stored: it is untrusted and may contain free-form reasoning.
            RecordSubmission(step, null, AgentToolStatus.Failed,
                $"{ex.Message} (output of {raw?.Length ?? 0} characters was not stored).", startedAt);
            throw;
        }
    }

    // ---- Review decisions ------------------------------------------------------

    public async Task<PromotionAgentWorkflowResponse?> ApproveAsync(
        Guid workflowId,
        int reviewerUserId,
        bool reviewerIsAdministrator,
        string? comment,
        CancellationToken cancellationToken = default)
    {
        var workflow = await LoadAsync(workflowId, cancellationToken);
        if (workflow is null)
        {
            return null;
        }

        var approval = PendingApproval(workflow, "approved");
        var document = LatestProposal(workflow)
            ?? throw new InvalidOperationException("There is no proposal to approve.");

        var impact = PromotionProposalValidator.AssessImpact(document, StoredPriceFacts(workflow), _options);
        if (impact == PromotionImpactLevel.High && !reviewerIsAdministrator)
        {
            throw new InvalidOperationException("High-impact proposals must be approved by an Administrator.");
        }

        await DecideAtomicallyAsync(workflow, approval, ApprovalStatus.Approved, reviewerUserId, comment,
            $"Approved by user {reviewerUserId}.", w => w.Status = AgentWorkflowStatus.InProgress, cancellationToken);

        _logger.LogInformation("Workflow {WorkflowId} approved by user {UserId}.", workflow.Id, reviewerUserId);

        AgentWorkflowStep? step = null;
        try
        {
            var input = ReadInput(workflow);

            // Data may have changed since the proposal: re-check before writing.
            step = await BeginStepAsync(workflow, PromotionAgentConstants.ValidatorName,
                "Re-validate against current data before execution", cancellationToken);
            var productIds = document.Proposals.Select(p => p.ProductId).ToList();
            var data = await GatherAsync(step, input.AnalysisDays, productIds, cancellationToken);
            var serverPricing = new Dictionary<Guid, JsonNode?>();
            await PriceProposalsAsync(step, document, serverPricing, cancellationToken);

            var facts = PromotionAgentData.Build(data.Velocity, data.Inventory, data.ActivePromotions, data.Details, data.Pricing);
            var outcomes = PromotionProposalValidator.Validate(
                    document,
                    new ProposalValidationInput(facts, serverPricing, input.MaxProposals, input.MaxDiscountPercent,
                        _options.MaxPromotionDays, Now(), input.ExcludeProductIds))
                // Evidence describes the analysis window and may drift; safety rules may not.
                .Where(o => o.Rule != "EvidenceMatchesData")
                .ToList();

            if (!RecordValidation(step, outcomes))
            {
                await FailAsync(workflow, step, "StaleProposal",
                    "The approved proposal no longer passes validation with current data; nothing was changed.");
                return await GetAsync(workflow.Id, CancellationToken.None);
            }

            await CompleteStepAsync(step, "Proposal is still valid.", cancellationToken);

            step = await BeginStepAsync(workflow, PromotionAgentConstants.ExecutorName,
                "Create approved promotions", cancellationToken);
            var startedAt = Now();
            var requests = document.Proposals.Select(p => ToPromotionRequest(p, workflow.Id)).ToList();
            var created = await _promotionService.CreatePromotionsAsync(requests, cancellationToken);

            step.ToolExecutions.Add(new AgentToolExecution
            {
                ToolName = PromotionAgentConstants.ApprovedAction,
                ToolArgumentsJson = JsonSerializer.Serialize(
                    requests.Select(r => new { r.Name, r.Type, r.DiscountValue, r.StartDate, r.EndDate, r.ProductIds }),
                    AgentJson.Options),
                ToolResultJson = JsonSerializer.Serialize(
                    new { PromotionIds = created.Select(c => c.Id) }, AgentJson.Options),
                Status = AgentToolStatus.Success,
                StartedAt = startedAt,
                CompletedAt = Now()
            });

            await CompleteStepAsync(step, $"Created {created.Count} promotion(s).", cancellationToken);

            workflow.Status = AgentWorkflowStatus.Completed;
            workflow.CompletedAt = Now();
            workflow.FinalOutcome = Truncate(
                $"Created {created.Count} promotion(s) after approval by user {reviewerUserId}: "
                + string.Join("; ", created.Select(c => c.Name)) + ".", 4000);
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Workflow {WorkflowId} created {Count} promotion(s).", workflow.Id, created.Count);
        }
        catch (Exception ex)
        {
            await FailSafelyAsync(workflow, step, ex);
        }

        return await GetAsync(workflow.Id, CancellationToken.None);
    }

    public async Task<PromotionAgentWorkflowResponse?> RejectAsync(
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
        await DecideAtomicallyAsync(workflow, approval, ApprovalStatus.Rejected, reviewerUserId, comment,
            $"Rejected by user {reviewerUserId}.", w =>
            {
                w.Status = AgentWorkflowStatus.Cancelled;
                w.CompletedAt = Now();
                w.FinalOutcome = "Proposal rejected by the reviewer; nothing was changed.";
            }, cancellationToken);

        _logger.LogInformation("Workflow {WorkflowId} rejected by user {UserId}.", workflow.Id, reviewerUserId);

        return await GetAsync(workflow.Id, cancellationToken);
    }

    public async Task<PromotionAgentWorkflowResponse?> ReviseAsync(
        Guid workflowId,
        int reviewerUserId,
        RevisePromotionAgentRequest request,
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
                $"The revision limit ({_options.MaxRevisions}) has been reached. Approve or reject the proposal.");
        }

        var comment = request.Comment.Trim();
        var current = ReadInput(workflow);
        var input = current with
        {
            MaxDiscountPercent = request.MaxDiscountPercent ?? current.MaxDiscountPercent,
            MaxProposals = request.MaxProposals ?? current.MaxProposals,
            ExcludeProductIds = current.ExcludeProductIds
                .Union(request.ExcludeProductIds.Where(id => id != Guid.Empty))
                .ToList()
        };

        await DecideAtomicallyAsync(workflow, approval, ApprovalStatus.RevisionRequested, reviewerUserId, comment,
            $"Revision requested by user {reviewerUserId}.", w =>
            {
                w.PlanSummary = SerializePlan(input);
                w.Status = AgentWorkflowStatus.Planning;
            }, cancellationToken);

        _logger.LogInformation("Workflow {WorkflowId} revision {Revision} requested by user {UserId}.",
            workflow.Id, revisions + 1, reviewerUserId);

        await RunProposalCycleAsync(workflow, input, comment, cancellationToken);

        return await GetAsync(workflow.Id, CancellationToken.None);
    }

    // ---- Queries ----------------------------------------------------------------

    public async Task<PromotionAgentWorkflowResponse?> GetAsync(Guid workflowId, CancellationToken cancellationToken = default)
    {
        var workflow = await LoadAsync(workflowId, cancellationToken, tracking: false);
        return workflow is null ? null : ToResponse(workflow);
    }

    public async Task<List<PromotionAgentWorkflowSummary>> ListAsync(int limit, CancellationToken cancellationToken = default)
    {
        return await _context.AgentWorkflows
            .AsNoTracking()
            .Where(w => w.Steps.Any(s => s.AgentName == PromotionAgentConstants.AgentName))
            .OrderByDescending(w => w.CreatedAt)
            .Take(Math.Clamp(limit, 1, 100))
            .Select(w => new PromotionAgentWorkflowSummary
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

    private async Task<AgentWorkflow?> LoadAsync(Guid workflowId, CancellationToken cancellationToken, bool tracking = true)
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

        // Only workflows run by this agent are visible here.
        return workflow is not null && workflow.Steps.Any(s => s.AgentName == PromotionAgentConstants.AgentName)
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
        PromotionProposalDocument? document,
        AgentToolStatus status,
        string? error,
        DateTime startedAt)
    {
        step.ToolExecutions.Add(new AgentToolExecution
        {
            ToolName = PromotionAgentConstants.SubmitProposal,
            ToolArgumentsJson = document is null
                ? null
                : AgentJson.Serialize(JsonRedactor.Redact(AgentJson.ToNode(document))),
            ToolResultJson = JsonSerializer.Serialize(
                new { Model = _model.Name, ProposalCount = document?.Proposals.Count ?? 0 }, AgentJson.Options),
            Status = status,
            ErrorMessage = error is null ? null : Truncate(error, 2000),
            StartedAt = startedAt,
            CompletedAt = Now()
        });
    }

    /// <summary>
    /// Records a review decision so that only one reviewer can decide an
    /// approval. The conditional UPDATE matches only while the approval is
    /// still pending, so of two concurrent reviews (two reviewers, or a double
    /// click) exactly one wins and the other gets a conflict before anything
    /// is created. The check on the loaded entity alone is not enough: both
    /// requests can read "pending" before either one saves.
    /// </summary>
    private async Task DecideAtomicallyAsync(
        AgentWorkflow workflow,
        AgentApproval approval,
        ApprovalStatus status,
        int reviewerUserId,
        string? comment,
        string stepSummary,
        Action<AgentWorkflow> updateWorkflow,
        CancellationToken cancellationToken)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        var claimed = await _context.AgentApprovals
            .Where(a => a.Id == approval.Id && a.Status == ApprovalStatus.Pending)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.Status, status), cancellationToken);

        if (claimed == 0)
        {
            throw new InvalidOperationException(
                "This proposal has already been reviewed. Reload the workflow to see the decision.");
        }

        Decide(approval, status, reviewerUserId, comment, stepSummary);
        updateWorkflow(workflow);
        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
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
        workflow.FinalOutcome = $"Failed safely ({errorType}); no promotions were created.";

        await _context.SaveChangesAsync(CancellationToken.None);

        _logger.LogWarning("Workflow {WorkflowId} failed: {ErrorType}.", workflow.Id, errorType);
    }

    /// <summary>
    /// Maps any failure to a safe, persisted error. Internal details go to the
    /// server log only. Business entities left pending by a failed write are
    /// discarded so nothing partial is saved.
    /// </summary>
    private async Task FailSafelyAsync(AgentWorkflow workflow, AgentWorkflowStep? step, Exception ex)
    {
        var (type, message) = ex switch
        {
            ToolPermissionException p => ("ToolNotPermitted", p.Message),
            PromotionAgentToolException { IsTimeout: true } t => ("ToolTimeout", t.Message),
            PromotionAgentToolException t => ("ToolFailure", t.Message),
            ProposalSchemaException s => ("MalformedOutput", $"The proposal was rejected: {s.Message}"),
            ModelTimeoutException => ("ModelTimeout", "The proposal model did not respond in time."),
            _ => ("UnexpectedError", "The agent stopped because of an unexpected error. Nothing was changed.")
        };

        _logger.LogError(ex, "Workflow {WorkflowId} stopped with {ErrorType}.", workflow.Id, type);

        foreach (var entry in _context.ChangeTracker.Entries()
                     .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
                     .Where(e => e.Entity is not (AgentWorkflow or AgentWorkflowStep or AgentToolExecution
                         or AgentValidationResult or AgentApproval or AgentWorkflowError))
                     .ToList())
        {
            entry.State = EntityState.Detached;
        }

        await FailAsync(workflow, step, type, message);
    }

    // ---- Stored data readers ----------------------------------------------------------

    private sealed record AgentInput(
        PromotionAgentFocus Focus,
        int AnalysisDays,
        int MaxProposals,
        int MaxDiscountPercent,
        List<Guid> ExcludeProductIds);

    private sealed record PlanDocument(string Agent, List<string> Plan, AgentInput Input);

    private static string SerializePlan(AgentInput input) =>
        JsonSerializer.Serialize(new PlanDocument(PromotionAgentConstants.AgentName, Plan.ToList(), input), AgentJson.Options);

    private static PlanDocument? ReadPlan(AgentWorkflow workflow)
    {
        try
        {
            return string.IsNullOrWhiteSpace(workflow.PlanSummary)
                ? null
                : JsonSerializer.Deserialize<PlanDocument>(workflow.PlanSummary, AgentJson.Options);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static AgentInput ReadInput(AgentWorkflow workflow) =>
        ReadPlan(workflow)?.Input ?? throw new InvalidOperationException("The workflow input could not be read.");

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

    private static PromotionProposalDocument? LatestProposal(AgentWorkflow workflow)
    {
        var latest = Executions(workflow, PromotionAgentConstants.SubmitProposal).LastOrDefault().Execution;

        return latest?.ToolArgumentsJson is null
            ? null
            : JsonSerializer.Deserialize<PromotionProposalDocument>(latest.ToolArgumentsJson, AgentJson.Options);
    }

    private static List<ProposalPricingResponse> LatestPricing(AgentWorkflow workflow)
    {
        var latestByProduct = new Dictionary<Guid, ProposalPricingResponse>();

        foreach (var (_, execution) in Executions(workflow, PromotionAgentConstants.CalculatePromotion))
        {
            var pricing = execution.ToolResultJson is null
                ? null
                : JsonSerializer.Deserialize<ProposalPricingResponse>(execution.ToolResultJson, AgentJson.Options);

            if (pricing is not null)
            {
                latestByProduct[pricing.ProductId] = pricing;
            }
        }

        return latestByProduct.Values.ToList();
    }

    /// <summary>Minimal facts (cheapest price) needed to assess impact.</summary>
    private static Dictionary<Guid, ProductFacts> StoredPriceFacts(AgentWorkflow workflow) =>
        LatestPricing(workflow).ToDictionary(
            p => p.ProductId,
            p => new ProductFacts(p.ProductId, string.Empty, true, false, 0, 0, 0, 0,
                p.Variants.Count == 0 ? 0m : p.Variants.Min(v => v.OriginalPrice)));

    private static List<Guid> CreatedPromotionIds(AgentWorkflow workflow)
    {
        var execution = Executions(workflow, PromotionAgentConstants.ApprovedAction).LastOrDefault().Execution;

        if (execution?.ToolResultJson is null)
        {
            return new List<Guid>();
        }

        return JsonNode.Parse(execution.ToolResultJson)?["promotionIds"]?
            .Deserialize<List<Guid>>(AgentJson.Options) ?? new List<Guid>();
    }

    private PromotionAgentWorkflowResponse ToResponse(AgentWorkflow workflow)
    {
        var steps = workflow.Steps.OrderBy(s => s.StepOrder).ToList();
        var proposal = LatestProposal(workflow);
        var hasProposals = proposal is { Proposals.Count: > 0 };

        return new PromotionAgentWorkflowResponse
        {
            WorkflowId = workflow.Id,
            Objective = workflow.Objective,
            Status = workflow.Status,
            Plan = ReadPlan(workflow)?.Plan ?? new List<string>(),
            Proposal = proposal,
            ImpactLevel = hasProposals
                ? PromotionProposalValidator.AssessImpact(proposal!, StoredPriceFacts(workflow), _options)
                : null,
            Pricing = LatestPricing(workflow),
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
            CreatedPromotionIds = CreatedPromotionIds(workflow),
            FinalOutcome = workflow.FinalOutcome,
            StartedAt = workflow.StartedAt,
            CompletedAt = workflow.CompletedAt
        };
    }

    private static PromotionRequest ToPromotionRequest(PromotionProposalItem proposal, Guid workflowId)
    {
        var offer = proposal.PromotionType == "PercentageDiscount"
            ? $"{proposal.DiscountValue:0.##}% off"
            : $"LKR {proposal.DiscountValue:0.00} off";

        return new PromotionRequest
        {
            Name = Truncate($"{proposal.ProductName} {offer}", 200),
            Description = Truncate(
                $"Proposed by the {PromotionAgentConstants.AgentName} (workflow {workflowId}). {proposal.Rationale}", 2000),
            Type = Enum.Parse<PromotionType>(proposal.PromotionType),
            DiscountValue = proposal.DiscountValue,
            StartDate = proposal.StartDate,
            EndDate = proposal.EndDate,
            IsActive = true,
            CampaignId = null,
            ProductIds = new List<Guid> { proposal.ProductId }
        };
    }

    private static int Count(JsonNode node, string property) => node[property]?.AsArray().Count ?? 0;

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
