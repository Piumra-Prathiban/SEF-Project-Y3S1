using System.ComponentModel.DataAnnotations;
using System.Text.Json.Nodes;
using SEF_Project.Api.Models.Enums;

namespace SEF_Project.Api.DTOs.Agents;

/// <summary>What kind of products the agent should look for.</summary>
public enum PromotionAgentFocus
{
    /// <summary>Sales falling versus the previous period.</summary>
    DecliningSales,

    /// <summary>Selling slowly relative to available stock (many days of cover).</summary>
    SlowMoving
}

public enum PromotionImpactLevel
{
    Low,
    High
}

// ---- Structured input contract ------------------------------------------------

public class StartPromotionAgentRequest
{
    /// <summary>E.g. "Find products with declining sales and recommend suitable promotions."</summary>
    [Required]
    [StringLength(500, MinimumLength = 10)]
    public string Objective { get; set; } = string.Empty;

    [EnumDataType(typeof(PromotionAgentFocus))]
    public PromotionAgentFocus Focus { get; set; } = PromotionAgentFocus.DecliningSales;

    /// <summary>Sales window compared with the previous window of the same length.</summary>
    [Range(7, 90)]
    public int AnalysisDays { get; set; } = 30;

    [Range(1, 10)]
    public int MaxProposals { get; set; } = 5;

    /// <summary>Upper bound for any proposed discount, as % of the item price.</summary>
    [Range(1, 50)]
    public int MaxDiscountPercent { get; set; } = 30;
}

public class ReviewPromotionAgentRequest
{
    [StringLength(1000)]
    public string? Comment { get; set; }
}

/// <summary>Reviewer asks the agent to produce a new proposal under tighter limits.</summary>
public class RevisePromotionAgentRequest
{
    [Required]
    [StringLength(1000, MinimumLength = 3)]
    public string Comment { get; set; } = string.Empty;

    [Range(1, 50)]
    public int? MaxDiscountPercent { get; set; }

    [Range(1, 10)]
    public int? MaxProposals { get; set; }

    public List<Guid> ExcludeProductIds { get; set; } = new();
}

// ---- Structured output contract (what the model must return) -------------------

/// <summary>
/// The only output the proposal model may produce. It is parsed strictly and
/// then validated deterministically against tool data before anyone sees it.
/// </summary>
public class PromotionProposalDocument
{
    public string SchemaVersion { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    public List<PromotionProposalItem> Proposals { get; set; } = new();
}

public class PromotionProposalItem
{
    public Guid ProductId { get; set; }

    public string ProductName { get; set; } = string.Empty;

    /// <summary>"PercentageDiscount" or "FixedAmountDiscount".</summary>
    public string PromotionType { get; set; } = string.Empty;

    public decimal DiscountValue { get; set; }

    public DateTime StartDate { get; set; }

    public DateTime EndDate { get; set; }

    /// <summary>Short factual justification that cites the evidence below.</summary>
    public string Rationale { get; set; } = string.Empty;

    public ProposalEvidence Evidence { get; set; } = new();
}

/// <summary>Figures the proposal is based on; must match tool results exactly.</summary>
public class ProposalEvidence
{
    public int UnitsSold { get; set; }

    public int PreviousUnitsSold { get; set; }

    public int AvailableQuantity { get; set; }
}

// ---- Workflow responses ----------------------------------------------------------

public class PromotionAgentWorkflowResponse
{
    public Guid WorkflowId { get; set; }

    public string Objective { get; set; } = string.Empty;

    public AgentWorkflowStatus Status { get; set; }

    public List<string> Plan { get; set; } = new();

    public PromotionImpactLevel? ImpactLevel { get; set; }

    public PromotionProposalDocument? Proposal { get; set; }

    /// <summary>Server-calculated prices for each proposal (CalculatePromotion tool).</summary>
    public List<ProposalPricingResponse> Pricing { get; set; } = new();

    public List<WorkflowStepResponse> Steps { get; set; } = new();

    public List<ToolExecutionResponse> ToolExecutions { get; set; } = new();

    public List<ValidationResultResponse> ValidationResults { get; set; } = new();

    public List<ApprovalResponse> Approvals { get; set; } = new();

    public List<WorkflowErrorResponse> Errors { get; set; } = new();

    public List<Guid> CreatedPromotionIds { get; set; } = new();

    public string? FinalOutcome { get; set; }

    public DateTime? StartedAt { get; set; }

    public DateTime? CompletedAt { get; set; }
}

public class ProposalPricingResponse
{
    public Guid ProductId { get; set; }

    public List<VariantPricingResponse> Variants { get; set; } = new();
}

public class VariantPricingResponse
{
    public Guid ProductVariantId { get; set; }

    public string Sku { get; set; } = string.Empty;

    public decimal OriginalPrice { get; set; }

    public decimal DiscountAmount { get; set; }

    public decimal FinalPrice { get; set; }
}

public class WorkflowStepResponse
{
    public int StepOrder { get; set; }

    public string AgentName { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public AgentStepStatus Status { get; set; }

    public string? Summary { get; set; }
}

public class ToolExecutionResponse
{
    public int StepOrder { get; set; }

    public string ToolName { get; set; } = string.Empty;

    public AgentToolStatus Status { get; set; }

    public JsonNode? Arguments { get; set; }

    public JsonNode? Result { get; set; }

    public string? ErrorMessage { get; set; }

    public DateTime? StartedAt { get; set; }

    public DateTime? CompletedAt { get; set; }
}

public class ValidationResultResponse
{
    public int StepOrder { get; set; }

    public string ValidatorName { get; set; } = string.Empty;

    public bool IsValid { get; set; }

    public ValidationSeverity Severity { get; set; }

    public string? Message { get; set; }
}

public class ApprovalResponse
{
    public ApprovalStatus Status { get; set; }

    public DateTime RequestedAt { get; set; }

    public int? ReviewedByUserId { get; set; }

    public DateTime? ReviewedAt { get; set; }

    public string? Comment { get; set; }
}

public class WorkflowErrorResponse
{
    public string ErrorType { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public DateTime OccurredAt { get; set; }
}

public class PromotionAgentWorkflowSummary
{
    public Guid WorkflowId { get; set; }

    public string Objective { get; set; } = string.Empty;

    public AgentWorkflowStatus Status { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? CompletedAt { get; set; }
}
