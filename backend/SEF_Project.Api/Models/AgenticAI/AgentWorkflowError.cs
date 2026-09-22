namespace SEF_Project.Api.Models.AgenticAI;

public class AgentWorkflowError : GuidEntity
{
    public Guid WorkflowId { get; set; }

    public AgentWorkflow Workflow { get; set; } = null!;

    public Guid? StepId { get; set; }

    public AgentWorkflowStep? Step { get; set; }

    public string ErrorType { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public DateTime OccurredAt { get; set; }
}
