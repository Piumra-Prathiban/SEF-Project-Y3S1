using SEF_Project.Api.Models.Enums;

namespace SEF_Project.Api.Models.AgenticAI;

public class AgentToolExecution : GuidEntity
{
    public Guid StepId { get; set; }

    public AgentWorkflowStep Step { get; set; } = null!;

    public string ToolName { get; set; } = string.Empty;

    public string? ToolArgumentsJson { get; set; }

    public string? ToolResultJson { get; set; }

    public AgentToolStatus Status { get; set; }

    public string? ErrorMessage { get; set; }

    public DateTime? StartedAt { get; set; }

    public DateTime? CompletedAt { get; set; }
}
