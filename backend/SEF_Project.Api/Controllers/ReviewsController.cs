using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SEF_Project.Api.DTOs.Shopping;
using SEF_Project.Api.Services.Shopping;

namespace SEF_Project.Api.Controllers;

/// <summary>
/// Product reviews and ratings. The read endpoint is anonymous (the storefront
/// product page is public and only ever exposes published reviews); writing is
/// limited to the authenticated customer who owns the review, and moderation is
/// limited to Staff and Administrators.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ReviewsController : ControllerBase
{
    private readonly IReviewService _reviewService;

    public ReviewsController(IReviewService reviewService)
    {
        _reviewService = reviewService;
    }

    /// <summary>Gets the published reviews and aggregate rating for a product.</summary>
    /// <response code="200">Returns the aggregate and the published reviews.</response>
    [HttpGet("products/{productId:guid}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ProductReviewsResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ProductReviewsResponse>> GetProductReviews(
        Guid productId,
        CancellationToken cancellationToken)
    {
        var response = await _reviewService.GetPublishedReviewsAsync(
            productId,
            cancellationToken);

        return Ok(response);
    }

    /// <summary>
    /// Writes the authenticated customer's review of a product. Writing again
    /// updates their existing review rather than creating a duplicate.
    /// </summary>
    /// <response code="201">The review was created.</response>
    /// <response code="200">The customer's existing review was updated.</response>
    /// <response code="400">The rating is outside 1-5 or the comment is too long.</response>
    /// <response code="401">The request is not authenticated as an active customer.</response>
    /// <response code="404">The active product does not exist.</response>
    [HttpPut("products/{productId:guid}")]
    [Authorize]
    [ProducesResponseType(typeof(ReviewResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ReviewResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ReviewResponse>> SaveReview(
        Guid productId,
        SaveReviewRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
        {
            return Unauthorized();
        }

        var result = await _reviewService.SaveReviewAsync(
            userId.Value,
            productId,
            request,
            cancellationToken);

        return result.Status switch
        {
            SaveReviewStatus.Created => StatusCode(
                StatusCodes.Status201Created,
                result.Review),
            SaveReviewStatus.Updated => Ok(result.Review),
            SaveReviewStatus.ProductNotFound => Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Product not found",
                detail: "The requested active product does not exist."),
            _ => throw new InvalidOperationException(
                "Unexpected review operation result.")
        };
    }

    /// <summary>Gets the authenticated customer's own review for a product.</summary>
    /// <response code="200">Returns the customer's review.</response>
    /// <response code="401">The request is not authenticated.</response>
    /// <response code="404">The customer has not reviewed this product.</response>
    [HttpGet("products/{productId:guid}/mine")]
    [Authorize]
    [ProducesResponseType(typeof(ReviewResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ReviewResponse>> GetOwnReview(
        Guid productId,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
        {
            return Unauthorized();
        }

        var review = await _reviewService.GetOwnReviewAsync(
            userId.Value,
            productId,
            cancellationToken);

        return review is null ? NotFound() : Ok(review);
    }

    /// <summary>Deletes the authenticated customer's own review for a product.</summary>
    /// <response code="204">The review was deleted.</response>
    /// <response code="401">The request is not authenticated.</response>
    /// <response code="404">The customer has not reviewed this product.</response>
    [HttpDelete("products/{productId:guid}")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteOwnReview(
        Guid productId,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
        {
            return Unauthorized();
        }

        var removed = await _reviewService.DeleteOwnReviewAsync(
            userId.Value,
            productId,
            cancellationToken);

        return removed
            ? NoContent()
            : Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Review not found",
                detail: "You have not reviewed this product.");
    }

    /// <summary>Lists reviews for moderation, with product, state and rating filters.</summary>
    /// <response code="200">Returns a page of reviews matching the filters.</response>
    /// <response code="403">The caller is not Staff or an Administrator.</response>
    [HttpGet]
    [Authorize(Roles = "Staff,Administrator")]
    [ProducesResponseType(typeof(StaffReviewListResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<StaffReviewListResponse>> GetReviewsForModeration(
        [FromQuery] StaffReviewQuery query,
        CancellationToken cancellationToken)
    {
        var response = await _reviewService.GetReviewsForModerationAsync(
            query,
            cancellationToken);

        return Ok(response);
    }

    /// <summary>Hides or unhides a review.</summary>
    /// <response code="200">Returns the updated review.</response>
    /// <response code="403">The caller is not Staff or an Administrator.</response>
    /// <response code="404">No review has the supplied identifier.</response>
    [HttpPatch("{reviewId:guid}/published")]
    [Authorize(Roles = "Staff,Administrator")]
    [ProducesResponseType(typeof(ReviewResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ReviewResponse>> SetPublished(
        Guid reviewId,
        SetReviewPublishedRequest request,
        CancellationToken cancellationToken)
    {
        var review = await _reviewService.SetPublishedAsync(
            reviewId,
            request.IsPublished,
            cancellationToken);

        return review is null ? NotFound() : Ok(review);
    }

    /// <summary>Deletes any review.</summary>
    /// <response code="204">The review was deleted.</response>
    /// <response code="403">The caller is not Staff or an Administrator.</response>
    /// <response code="404">No review has the supplied identifier.</response>
    [HttpDelete("{reviewId:guid}")]
    [Authorize(Roles = "Staff,Administrator")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteReview(
        Guid reviewId,
        CancellationToken cancellationToken)
    {
        var removed = await _reviewService.DeleteReviewAsync(
            reviewId,
            cancellationToken);

        return removed ? NoContent() : NotFound();
    }

    private int? GetCurrentUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        return int.TryParse(claim, out var userId)
            ? userId
            : null;
    }
}
