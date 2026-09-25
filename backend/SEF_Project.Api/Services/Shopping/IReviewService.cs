using SEF_Project.Api.DTOs.Shopping;

namespace SEF_Project.Api.Services.Shopping;

/// <summary>Outcome of a create/update review request.</summary>
public enum SaveReviewStatus
{
    /// <summary>A new review was written for the customer.</summary>
    Created,

    /// <summary>The customer already had a review, which was updated in place.</summary>
    Updated,

    /// <summary>The product does not exist or is not active and cannot be reviewed.</summary>
    ProductNotFound
}

/// <summary>Result of a create/update review request.</summary>
public record SaveReviewResult(
    SaveReviewStatus Status,
    ReviewResponse? Review = null);

/// <summary>
/// Product reviews and ratings: public reads, customer writes and staff
/// moderation. One review per customer per product.
/// </summary>
public interface IReviewService
{
    /// <summary>
    /// Returns the published reviews for a product (newest first) together with
    /// its aggregate rating. Unpublished reviews are never included.
    /// </summary>
    Task<ProductReviewsResponse> GetPublishedReviewsAsync(
        Guid productId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates the customer's review for an active product, or updates their
    /// existing review if they have already reviewed it.
    /// </summary>
    Task<SaveReviewResult> SaveReviewAsync(
        int userId,
        Guid productId,
        SaveReviewRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the customer's own review for a product, or <c>null</c> when they
    /// have not reviewed it. The review is returned regardless of its published
    /// state so the author can see and manage it.
    /// </summary>
    Task<ReviewResponse?> GetOwnReviewAsync(
        int userId,
        Guid productId,
        CancellationToken cancellationToken = default);

    /// <summary>Deletes the customer's own review for a product.</summary>
    Task<bool> DeleteOwnReviewAsync(
        int userId,
        Guid productId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists reviews for staff moderation, filtered by product, published state
    /// and minimum rating, and paged.
    /// </summary>
    Task<StaffReviewListResponse> GetReviewsForModerationAsync(
        StaffReviewQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Hides or unhides a review. Returns the updated review, or <c>null</c>
    /// when no review has that identifier.
    /// </summary>
    Task<ReviewResponse?> SetPublishedAsync(
        Guid reviewId,
        bool isPublished,
        CancellationToken cancellationToken = default);

    /// <summary>Deletes any review. Returns <c>false</c> when it does not exist.</summary>
    Task<bool> DeleteReviewAsync(
        Guid reviewId,
        CancellationToken cancellationToken = default);
}
