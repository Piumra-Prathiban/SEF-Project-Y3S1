namespace SEF_Project.Api.Services.Recommendations;

public interface IAgentWorkflowRecorder
{
    Task<AgentWorkflowHandle> StartAsync(
        string objective,
        CancellationToken cancellationToken = default);

    Task<AgentToolExecutionHandle> StartToolAsync(
        AgentWorkflowHandle workflow,
        string toolName,
        string argumentsJson,
        CancellationToken cancellationToken = default);

    Task CompleteToolAsync(
        AgentToolExecutionHandle execution,
        bool succeeded,
        string? resultJson,
        string? errorSummary,
        CancellationToken cancellationToken = default);

    Task RecordValidationAsync(
        AgentWorkflowHandle workflow,
        RecommendationValidationCheck check,
        CancellationToken cancellationToken = default);

    Task CompleteAsync(
        AgentWorkflowHandle workflow,
        string finalResultJson,
        CancellationToken cancellationToken = default);

    Task FailAsync(
        AgentWorkflowHandle workflow,
        string errorType,
        string errorSummary,
        CancellationToken cancellationToken = default);
}
