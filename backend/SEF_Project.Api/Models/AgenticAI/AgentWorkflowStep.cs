using SEF_Project.Api.Models.Enums;

namespace SEF_Project.Api.Models.AgenticAI;

public class AgentWorkflowStep : GuidEntity
{
    public Guid WorkflowId { get; set; }

    public AgentWorkflow Workflow { get; set; } = null!;

    public int StepOrder { get; set; }

    public string AgentName { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public AgentStepStatus Status { get; set; }

    public string? Summary { get; set; }

    public DateTime? StartedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public ICollection<AgentToolExecution> ToolExecutions { get; set; } =
        new List<AgentToolExecution>();

    public ICollection<AgentValidationResult> ValidationResults { get; set; } =
        new List<AgentValidationResult>();
}
