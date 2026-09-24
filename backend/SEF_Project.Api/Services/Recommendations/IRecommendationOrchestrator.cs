namespace SEF_Project.Api.Services.Recommendations;

/// <summary>
/// Seam for the future Personal Stylist Agent. Implementations can orchestrate
/// only the controlled tools supplied by the application layer.
/// </summary>
public interface IRecommendationOrchestrator
{
    Task<RecommendationOrchestrationResult> CreateCandidatesAsync(
        int userId,
        RecommendationContext context,
        CancellationToken cancellationToken = default);
}
