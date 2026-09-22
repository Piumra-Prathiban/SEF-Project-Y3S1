using SEF_Project.Api.Models.Enums;

namespace SEF_Project.Api.Models.AgenticAI;

public class AgentValidationResult : GuidEntity
{
    public Guid StepId { get; set; }

    public AgentWorkflowStep Step { get; set; } = null!;

    public bool IsValid { get; set; }

    public string ValidatorName { get; set; } = string.Empty;

    public string? Message { get; set; }

    public ValidationSeverity Severity { get; set; }
}
