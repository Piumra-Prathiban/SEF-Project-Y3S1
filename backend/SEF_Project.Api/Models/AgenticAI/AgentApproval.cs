using SEF_Project.Api.Models.Enums;

namespace SEF_Project.Api.Models.AgenticAI;

public class AgentApproval : GuidEntity
{
    public Guid WorkflowId { get; set; }

    public AgentWorkflow Workflow { get; set; } = null!;

    public Guid? StepId { get; set; }

    public AgentWorkflowStep? Step { get; set; }

    public ApprovalStatus Status { get; set; }

    public DateTime RequestedAt { get; set; }

    public int? ReviewedByUserId { get; set; }

    public User? ReviewedByUser { get; set; }

    public DateTime? ReviewedAt { get; set; }

    public string? Comment { get; set; }
}
