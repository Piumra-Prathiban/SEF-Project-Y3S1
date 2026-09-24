using SEF_Project.Api.DTOs.Common;
using SEF_Project.Api.DTOs.Marketing;

namespace SEF_Project.Api.Services.Marketing;

public interface IPromotionService
{
    /// <summary>
    /// Staff see every promotion; everyone else only sees promotions that are
    /// currently live (active, within dates, campaign active).
    /// </summary>
    Task<PagedResponse<PromotionResponse>> GetPromotionsAsync(
        bool canManagePromotions,
        PromotionQuery query,
        CancellationToken cancellationToken = default);

    Task<PromotionResponse?> GetPromotionByIdAsync(
        bool canManagePromotions,
        Guid promotionId,
        CancellationToken cancellationToken = default);

    Task<PromotionResponse> CreatePromotionAsync(
        PromotionRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Creates several promotions atomically (all or none).</summary>
    Task<List<PromotionResponse>> CreatePromotionsAsync(
        IReadOnlyList<PromotionRequest> requests,
        CancellationToken cancellationToken = default);

    Task<PromotionResponse?> UpdatePromotionAsync(
        Guid promotionId,
        PromotionRequest request,
        CancellationToken cancellationToken = default);

    Task<PromotionTargetsResponse> GetTargetOptionsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>Returns false when the promotion does not exist.</summary>
    Task<bool> DeletePromotionAsync(
        Guid promotionId,
        CancellationToken cancellationToken = default);
}
