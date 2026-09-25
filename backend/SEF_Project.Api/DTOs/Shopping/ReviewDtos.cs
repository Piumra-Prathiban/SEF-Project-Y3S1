using System.ComponentModel.DataAnnotations;

namespace SEF_Project.Api.DTOs.Shopping;

/// <summary>
/// Payload for writing the current customer's review of a product. The same
/// shape is used to create a review and to update the customer's existing one.
/// </summary>
public class SaveReviewRequest
{
    /// <summary>Star rating for the product, between 1 and 5 inclusive.</summary>
    [Range(1, 5)]
    public int Rating { get; set; }

    /// <summary>Optional written review, up to 2000 characters.</summary>
    [StringLength(2000)]
    public string? Comment { get; set; }
}

/// <summary>
/// A single product review. Customer emails and identifiers are never exposed;
/// reviewers are shown through a privacy-preserving display name.
/// </summary>
public class ReviewResponse
{
    public Guid Id { get; set; }

    public Guid ProductId { get; set; }

    /// <summary>Public reviewer name, e.g. "Asha P.". Never an email address.</summary>
    public string DisplayName { get; set; } = string.Empty;

    public int Rating { get; set; }

    public string? Comment { get; set; }

    public bool IsPublished { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}

/// <summary>How many published reviews awarded a particular star rating.</summary>
public class ReviewBreakdownItemResponse
{
    public int Rating { get; set; }

    public int Count { get; set; }
}

/// <summary>Star rating distribution across all five possible ratings.</summary>
public class ReviewBreakdownResponse
{
    public IReadOnlyList<ReviewBreakdownItemResponse> Items { get; set; } =
        Array.Empty<ReviewBreakdownItemResponse>();
}

/// <summary>
/// Aggregate rating information for a product: the average (rounded to one
/// decimal), the total number of published reviews and the star breakdown.
/// </summary>
public class ReviewAggregateResponse
{
    public decimal AverageRating { get; set; }

    public int TotalCount { get; set; }

    public ReviewBreakdownResponse Breakdown { get; set; } = new();
}

/// <summary>
/// Public storefront payload: the aggregate for a product plus its published
/// reviews, newest first.
/// </summary>
public class ProductReviewsResponse
{
    public Guid ProductId { get; set; }

    public ReviewAggregateResponse Aggregate { get; set; } = new();

    public IReadOnlyList<ReviewResponse> Reviews { get; set; } =
        Array.Empty<ReviewResponse>();
}

/// <summary>Filtering, sorting and paging options for staff review moderation.</summary>
public class StaffReviewQuery
{
    public Guid? ProductId { get; set; }

    public bool? IsPublished { get; set; }

    /// <summary>Only return reviews with at least this many stars (1-5).</summary>
    [Range(1, 5)]
    public int? MinRating { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 20;
}

/// <summary>Paged list of reviews returned to staff moderation tools.</summary>
public class StaffReviewListResponse
{
    public IReadOnlyList<ReviewResponse> Items { get; set; } =
        Array.Empty<ReviewResponse>();

    public int Page { get; set; }

    public int PageSize { get; set; }

    public int TotalItems { get; set; }

    public int TotalPages { get; set; }
}

/// <summary>Staff request to hide or unhide (publish) a review.</summary>
public class SetReviewPublishedRequest
{
    public bool IsPublished { get; set; }
}
