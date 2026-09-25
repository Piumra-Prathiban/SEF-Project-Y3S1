using Microsoft.EntityFrameworkCore;
using SEF_Project.Api.Data;
using SEF_Project.Api.DTOs.Shopping;
using SEF_Project.Api.Models;
using SEF_Project.Api.Models.Catalog;

namespace SEF_Project.Api.Services.Shopping;

/// <summary>
/// Product reviews and ratings. Customers may write and manage a single review
/// per product; staff moderate published state. Public reads only ever surface
/// published reviews and never leak reviewer identity beyond a display name.
/// </summary>
public class ReviewService : IReviewService
{
    private const int MaxCommentLength = 2000;
    private static readonly int[] StarValues = { 5, 4, 3, 2, 1 };

    private readonly AppDbContext _context;

    public ReviewService(AppDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task<ProductReviewsResponse> GetPublishedReviewsAsync(
        Guid productId,
        CancellationToken cancellationToken = default)
    {
        var reviews = await PublishedReviews()
            .Where(review => review.ProductId == productId)
            .OrderByDescending(review => review.CreatedAt)
            .ThenByDescending(review => review.Id)
            .ToListAsync(cancellationToken);

        return new ProductReviewsResponse
        {
            ProductId = productId,
            Aggregate = BuildAggregate(reviews),
            Reviews = reviews.Select(BuildReviewResponse).ToList()
        };
    }

    /// <inheritdoc />
    public async Task<SaveReviewResult> SaveReviewAsync(
        int userId,
        Guid productId,
        SaveReviewRequest request,
        CancellationToken cancellationToken = default)
    {
        if (productId == Guid.Empty)
        {
            throw new ArgumentException("A valid product identifier is required.");
        }

        if (request.Rating is < 1 or > 5)
        {
            throw new ArgumentException("Rating must be between 1 and 5.");
        }

        var comment = NormalizeComment(request.Comment);

        var customer = await ResolveCustomerAsync(userId, cancellationToken);

        var productExists = await _context.Products
            .AnyAsync(
                product => product.Id == productId && product.IsActive,
                cancellationToken);

        if (!productExists)
        {
            return new SaveReviewResult(SaveReviewStatus.ProductNotFound);
        }

        var review = await _context.Reviews
            .SingleOrDefaultAsync(
                existing =>
                    existing.ProductId == productId &&
                    existing.CustomerId == customer.Id,
                cancellationToken);

        if (review is null)
        {
            review = new Review
            {
                ProductId = productId,
                CustomerId = customer.Id,
                Rating = request.Rating,
                Comment = comment
            };

            _context.Reviews.Add(review);
            await _context.SaveChangesAsync(cancellationToken);

            return new SaveReviewResult(
                SaveReviewStatus.Created,
                BuildReviewResponse(review, customer.User));
        }

        review.Rating = request.Rating;
        review.Comment = comment;
        await _context.SaveChangesAsync(cancellationToken);

        return new SaveReviewResult(
            SaveReviewStatus.Updated,
            BuildReviewResponse(review, customer.User));
    }

    /// <inheritdoc />
    public async Task<ReviewResponse?> GetOwnReviewAsync(
        int userId,
        Guid productId,
        CancellationToken cancellationToken = default)
    {
        if (productId == Guid.Empty)
        {
            return null;
        }

        var review = await _context.Reviews
            .AsNoTracking()
            .Include(existing => existing.Customer)
                .ThenInclude(customer => customer.User)
            .SingleOrDefaultAsync(
                existing =>
                    existing.ProductId == productId &&
                    existing.Customer.UserId == userId,
                cancellationToken);

        return review is null ? null : BuildReviewResponse(review);
    }

    /// <inheritdoc />
    public async Task<bool> DeleteOwnReviewAsync(
        int userId,
        Guid productId,
        CancellationToken cancellationToken = default)
    {
        if (productId == Guid.Empty)
        {
            return false;
        }

        var review = await _context.Reviews
            .SingleOrDefaultAsync(
                existing =>
                    existing.ProductId == productId &&
                    existing.Customer.UserId == userId,
                cancellationToken);

        if (review is null)
        {
            return false;
        }

        _context.Reviews.Remove(review);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <inheritdoc />
    public async Task<StaffReviewListResponse> GetReviewsForModerationAsync(
        StaffReviewQuery query,
        CancellationToken cancellationToken = default)
    {
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var reviews = _context.Reviews
            .AsNoTracking()
            .Include(review => review.Customer)
                .ThenInclude(customer => customer.User)
            .AsQueryable();

        if (query.ProductId is not null)
        {
            reviews = reviews.Where(
                review => review.ProductId == query.ProductId.Value);
        }

        if (query.IsPublished is not null)
        {
            reviews = reviews.Where(
                review => review.IsPublished == query.IsPublished.Value);
        }

        if (query.MinRating is not null)
        {
            reviews = reviews.Where(
                review => review.Rating >= query.MinRating.Value);
        }

        var totalItems = await reviews.CountAsync(cancellationToken);

        var items = await reviews
            .OrderByDescending(review => review.CreatedAt)
            .ThenByDescending(review => review.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new StaffReviewListResponse
        {
            Items = items.Select(BuildReviewResponse).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalItems = totalItems,
            TotalPages = (int)Math.Ceiling(totalItems / (double)pageSize)
        };
    }

    /// <inheritdoc />
    public async Task<ReviewResponse?> SetPublishedAsync(
        Guid reviewId,
        bool isPublished,
        CancellationToken cancellationToken = default)
    {
        var review = await _context.Reviews
            .Include(existing => existing.Customer)
                .ThenInclude(customer => customer.User)
            .SingleOrDefaultAsync(
                existing => existing.Id == reviewId,
                cancellationToken);

        if (review is null)
        {
            return null;
        }

        review.IsPublished = isPublished;
        await _context.SaveChangesAsync(cancellationToken);

        return BuildReviewResponse(review);
    }

    /// <inheritdoc />
    public async Task<bool> DeleteReviewAsync(
        Guid reviewId,
        CancellationToken cancellationToken = default)
    {
        var review = await _context.Reviews
            .SingleOrDefaultAsync(
                existing => existing.Id == reviewId,
                cancellationToken);

        if (review is null)
        {
            return false;
        }

        _context.Reviews.Remove(review);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    private IQueryable<Review> PublishedReviews() =>
        _context.Reviews
            .AsNoTracking()
            .Where(review => review.IsPublished)
            .Include(review => review.Customer)
                .ThenInclude(customer => customer.User);

    private static ReviewAggregateResponse BuildAggregate(
        IReadOnlyCollection<Review> reviews)
    {
        var total = reviews.Count;

        var average = total == 0
            ? 0m
            : Math.Round(
                (decimal)reviews.Average(review => review.Rating),
                1,
                MidpointRounding.AwayFromZero);

        return new ReviewAggregateResponse
        {
            AverageRating = average,
            TotalCount = total,
            Breakdown = new ReviewBreakdownResponse
            {
                Items = StarValues
                    .Select(star => new ReviewBreakdownItemResponse
                    {
                        Rating = star,
                        Count = reviews.Count(review => review.Rating == star)
                    })
                    .ToList()
            }
        };
    }

    private static ReviewResponse BuildReviewResponse(Review review) =>
        BuildReviewResponse(review, review.Customer?.User);

    private static ReviewResponse BuildReviewResponse(Review review, User? user) =>
        new()
        {
            Id = review.Id,
            ProductId = review.ProductId,
            DisplayName = BuildDisplayName(user),
            Rating = review.Rating,
            Comment = review.Comment,
            IsPublished = review.IsPublished,
            CreatedAt = review.CreatedAt,
            UpdatedAt = review.UpdatedAt
        };

    private static string BuildDisplayName(User? user)
    {
        var firstName = user?.FirstName?.Trim();

        if (string.IsNullOrEmpty(firstName))
        {
            return "Clothic shopper";
        }

        var lastName = user!.LastName?.Trim();

        return string.IsNullOrEmpty(lastName)
            ? firstName
            : $"{firstName} {char.ToUpperInvariant(lastName[0])}.";
    }

    private static string? NormalizeComment(string? comment)
    {
        if (string.IsNullOrWhiteSpace(comment))
        {
            return null;
        }

        var trimmed = comment.Trim();

        if (trimmed.Length > MaxCommentLength)
        {
            throw new ArgumentException(
                $"Review comments cannot exceed {MaxCommentLength} characters.");
        }

        return trimmed;
    }

    private async Task<Customer> ResolveCustomerAsync(
        int userId,
        CancellationToken cancellationToken)
    {
        var customer = await _context.Customers
            .Include(existing => existing.User)
            .FirstOrDefaultAsync(
                existing => existing.UserId == userId,
                cancellationToken);

        if (customer is null)
        {
            throw new UnauthorizedAccessException(
                "An active customer profile is required.");
        }

        if (!customer.User.IsActive)
        {
            throw new UnauthorizedAccessException(
                "This account is inactive.");
        }

        return customer;
    }
}
