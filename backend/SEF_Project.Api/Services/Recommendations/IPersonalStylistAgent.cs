namespace SEF_Project.Api.Services.Recommendations;

/// <summary>
/// Domain-specific agent for fashion product recommendations. It accepts only
/// structured shopping context and returns only validated catalogue variants.
/// </summary>
public interface IPersonalStylistAgent
{
    Task<PersonalStylistAgentResult> RunAsync(
        int userId,
        RecommendationContext context,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the Personal Stylist as one delegated step in an existing
    /// shared workflow. The parent workflow remains open for downstream agents.
    /// </summary>
    Task<PersonalStylistAgentResult> RunWithinWorkflowAsync(
        Guid workflowId,
        int userId,
        RecommendationContext context,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Bounded reasoning component. Its output is untrusted until validated against
/// Product Availability Tool output by the agent.
/// </summary>
public interface IPersonalStylistRecommendationModel
{
    Task<PersonalStylistModelOutput> GenerateAsync(
        PersonalStylistModelInput input,
        CancellationToken cancellationToken = default);
}

public interface IPersonalStylistOutputValidator
{
    AgentOutputValidation Validate(
        PersonalStylistModelOutput? output,
        PersonalStylistModelInput input);
}
