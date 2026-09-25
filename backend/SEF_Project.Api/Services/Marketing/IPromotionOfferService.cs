using SEF_Project.Api.DTOs.Marketing;

namespace SEF_Project.Api.Services.Marketing;

/// <summary>
/// Customer-facing, read-only promotion offers with server-calculated prices.
/// Only live promotions (active, within dates, campaign active) are exposed.
/// </summary>
public interface IPromotionOfferService
{
    /// <summary>Returns null when the promotion does not exist or is not live.</summary>
    Task<PromotionProductsResponse?> GetPromotionProductsAsync(
        Guid promotionId,
        CancellationToken cancellationToken = default);

    /// <summary>Returns null when the product does not exist or is inactive.</summary>
    Task<ProductPromotionsResponse?> GetProductPromotionsAsync(
        Guid productId,
        CancellationToken cancellationToken = default);
}
