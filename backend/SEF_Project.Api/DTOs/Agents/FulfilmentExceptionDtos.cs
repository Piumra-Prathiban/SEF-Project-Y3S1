using System.ComponentModel.DataAnnotations;
using SEF_Project.Api.Models.Enums;

namespace SEF_Project.Api.DTOs.Agents;

/// <summary>
/// The only output the resolution model may produce. It is parsed strictly and
/// validated deterministically before anyone sees it.
/// </summary>
public class FulfilmentExceptionDocument
{
    public string SchemaVersion { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    public FulfilmentResolution Resolution { get; set; } = new();
}

public class FulfilmentResolution
{
    /// <summary>"Order", "Payment", "Shipment" or "None".</summary>
    public string TargetType { get; set; } = "None";

    public Guid? TargetId { get; set; }

    /// <summary>The proposed next status, e.g. "Confirmed", "Shipped", or "NoAction".</summary>
    public string Action { get; set; } = "NoAction";

    public string Rationale { get; set; } = string.Empty;

    public FulfilmentResolutionEvidence Evidence { get; set; } = new();
}

public class FulfilmentResolutionEvidence
{
    public string? CurrentStatus { get; set; }
}

// ---- Structured input contracts ------------------------------------------------

public class StartFulfilmentExceptionRequest
{
    /// <summary>E.g. "Resolve order ORD-… which appears stuck."</summary>
    [Required]
    [StringLength(500, MinimumLength = 10)]
    public string Objective { get; set; } = string.Empty;

    /// <summary>The order the agent should investigate.</summary>
    public Guid OrderId { get; set; }
}

public class ReviewFulfilmentExceptionRequest
{
    [StringLength(1000)]
    public string? Comment { get; set; }
}

/// <summary>Reviewer asks the agent to produce a new resolution.</summary>
public class ReviseFulfilmentExceptionRequest
{
    [Required]
    [StringLength(1000, MinimumLength = 3)]
    public string Comment { get; set; } = string.Empty;
}

// ---- Workflow responses ----------------------------------------------------------

public class FulfilmentExceptionWorkflowResponse
{
    public Guid WorkflowId { get; set; }

    public string Objective { get; set; } = string.Empty;

    public AgentWorkflowStatus Status { get; set; }

    public List<string> Plan { get; set; } = new();

    public FulfilmentExceptionDocument? Resolution { get; set; }

    public List<WorkflowStepResponse> Steps { get; set; } = new();

    public List<ToolExecutionResponse> ToolExecutions { get; set; } = new();

    public List<ValidationResultResponse> ValidationResults { get; set; } = new();

    public List<ApprovalResponse> Approvals { get; set; } = new();

    public List<WorkflowErrorResponse> Errors { get; set; } = new();

    public string? FinalOutcome { get; set; }

    public DateTime? StartedAt { get; set; }

    public DateTime? CompletedAt { get; set; }
}

public class FulfilmentExceptionWorkflowSummary
{
    public Guid WorkflowId { get; set; }

    public string Objective { get; set; } = string.Empty;

    public AgentWorkflowStatus Status { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? CompletedAt { get; set; }
}
