using SEF_Project.Api.Models.Enums;

namespace SEF_Project.Api.Models.AgenticAI;

public class AgentWorkflow : GuidEntity
{
    public string Objective { get; set; } = string.Empty;

    public AgentWorkflowStatus Status { get; set; }

    public string? PlanSummary { get; set; }

    public string? FinalOutcome { get; set; }

    public DateTime? StartedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public ICollection<AgentWorkflowStep> Steps { get; set; } =
        new List<AgentWorkflowStep>();

    public ICollection<AgentApproval> Approvals { get; set; } =
        new List<AgentApproval>();

    public ICollection<AgentWorkflowError> Errors { get; set; } =
        new List<AgentWorkflowError>();
}
