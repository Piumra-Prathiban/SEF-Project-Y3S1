using SEF_Project.Api.DTOs.Recommendations;

namespace SEF_Project.Api.Services.Recommendations;

public interface IRecommendationService
{
    Task<RecommendationResponse> StartAsync(
        int userId,
        RecommendationRequest request,
        CancellationToken cancellationToken = default);
}
