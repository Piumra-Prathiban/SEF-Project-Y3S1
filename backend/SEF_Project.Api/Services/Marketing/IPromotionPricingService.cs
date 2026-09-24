using SEF_Project.Api.DTOs.Marketing;

namespace SEF_Project.Api.Services.Marketing;

public interface IPromotionPricingService
{
    /// <summary>
    /// Returns null when the promotion or product variant does not exist.
    /// Throws <see cref="InvalidOperationException"/> when the promotion
    /// cannot be applied to the variant.
    /// </summary>
    Task<PromotionDiscountResponse?> CalculatePromotionDiscountAsync(
        Guid promotionId,
        CalculatePromotionDiscountRequest request,
        CancellationToken cancellationToken = default);
}
