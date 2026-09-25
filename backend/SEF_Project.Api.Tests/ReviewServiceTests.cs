using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SEF_Project.Api.Controllers;
using SEF_Project.Api.Data;
using SEF_Project.Api.Data.Configurations;
using SEF_Project.Api.DTOs.Shopping;
using SEF_Project.Api.Models;
using SEF_Project.Api.Models.Catalog;
using SEF_Project.Api.Services.Shopping;

namespace SEF_Project.Api.Tests;

public class ReviewServiceTests
{
    [Fact]
    public void ReviewsController_ShouldMarkPublicReadAnonymous_AndGuardWrites()
    {
        var controller = typeof(ReviewsController);

        var publicRead = controller.GetMethod(
            nameof(ReviewsController.GetProductReviews));

        Assert.NotNull(publicRead);
        Assert.NotNull(
            publicRead!.GetCustomAttribute<AllowAnonymousAttribute>());
        Assert.Null(publicRead.GetCustomAttribute<AuthorizeAttribute>());

        var customerWrite = controller.GetMethod(
            nameof(ReviewsController.SaveReview));

        var writeAuthorize =
            customerWrite!.GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(writeAuthorize);
        Assert.Null(writeAuthorize!.Roles);

        foreach (var moderationMethod in new[]
                 {
                     nameof(ReviewsController.GetReviewsForModeration),
                     nameof(ReviewsController.SetPublished),
                     nameof(ReviewsController.DeleteReview)
                 })
        {
            var authorize = controller
                .GetMethod(moderationMethod)!
                .GetCustomAttribute<AuthorizeAttribute>();

            Assert.NotNull(authorize);
            Assert.Equal("Staff,Administrator", authorize!.Roles);
        }
    }

    [Fact]
    public async Task SaveReview_CreatesReviewForActiveProduct()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var context = await CreateContextAsync(connection);
        var customer = await AddCustomerAsync(context, "create@test.com", "Asha", "Perera");
        var controller = CreateController(new ReviewService(context), customer.UserId);

        var action = await controller.SaveReview(
            SeedData.ProductTShirt,
            new SaveReviewRequest { Rating = 5, Comment = "  Lovely fit  " },
            CancellationToken.None);

        var created = Assert.IsType<ObjectResult>(action.Result);
        Assert.Equal(StatusCodes.Status201Created, created.StatusCode);

        var review = Assert.IsType<ReviewResponse>(created.Value);
        Assert.Equal(5, review.Rating);
        Assert.Equal("Lovely fit", review.Comment);
        Assert.Equal("Asha P.", review.DisplayName);
        Assert.True(review.IsPublished);
        Assert.DoesNotContain("@", review.DisplayName);

        var stored = await context.Reviews.SingleAsync();
        Assert.Equal(SeedData.ProductTShirt, stored.ProductId);
        Assert.Equal(customer.CustomerId, stored.CustomerId);
    }

    [Fact]
    public async Task SaveReview_SecondAttemptUpdatesInsteadOfDuplicating()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var context = await CreateContextAsync(connection);
        var customer = await AddCustomerAsync(context, "update@test.com", "Bimal", "Silva");
        var controller = CreateController(new ReviewService(context), customer.UserId);

        var first = await controller.SaveReview(
            SeedData.ProductHoodie,
            new SaveReviewRequest { Rating = 4, Comment = "Good" },
            CancellationToken.None);

        var second = await controller.SaveReview(
            SeedData.ProductHoodie,
            new SaveReviewRequest { Rating = 2, Comment = "Changed my mind" },
            CancellationToken.None);

        Assert.Equal(
            StatusCodes.Status201Created,
            Assert.IsType<ObjectResult>(first.Result).StatusCode);

        var updated = Assert.IsType<ReviewResponse>(
            Assert.IsType<OkObjectResult>(second.Result).Value);

        Assert.Equal(2, updated.Rating);
        Assert.Equal("Changed my mind", updated.Comment);
        Assert.Equal(1, await context.Reviews.CountAsync());

        var stored = await context.Reviews.SingleAsync();
        Assert.Equal(2, stored.Rating);
    }

    [Fact]
    public async Task SaveReview_ReturnsNotFoundForUnknownProduct()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var context = await CreateContextAsync(connection);
        var customer = await AddCustomerAsync(context, "unknown@test.com", "Chloe", "Fernando");
        var controller = CreateController(new ReviewService(context), customer.UserId);

        var action = await controller.SaveReview(
            Guid.NewGuid(),
            new SaveReviewRequest { Rating = 3 },
            CancellationToken.None);

        var result = Assert.IsType<ObjectResult>(action.Result);
        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
        Assert.Empty(context.Reviews);
    }

    [Fact]
    public async Task SaveReview_ReturnsNotFoundForInactiveProduct()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var context = await CreateContextAsync(connection);
        var customer = await AddCustomerAsync(context, "inactive@test.com", "Dan", "Perera");

        var boots = await context.Products
            .SingleAsync(product => product.Id == SeedData.ProductBoots);
        boots.IsActive = false;
        await context.SaveChangesAsync();

        var controller = CreateController(new ReviewService(context), customer.UserId);
        var action = await controller.SaveReview(
            SeedData.ProductBoots,
            new SaveReviewRequest { Rating = 4 },
            CancellationToken.None);

        var result = Assert.IsType<ObjectResult>(action.Result);
        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
        Assert.Empty(context.Reviews);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    [InlineData(-1)]
    public async Task SaveReview_RejectsRatingOutsideOneToFive(int rating)
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var context = await CreateContextAsync(connection);
        var customer = await AddCustomerAsync(context, "rating@test.com", "Eve", "Nair");
        var service = new ReviewService(context);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.SaveReviewAsync(
                customer.UserId,
                SeedData.ProductTShirt,
                new SaveReviewRequest { Rating = rating }));

        Assert.Empty(context.Reviews);
    }

    [Fact]
    public async Task GetPublishedReviews_ReturnsAggregatesBreakdownAndNewestFirst()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var context = await CreateContextAsync(connection);
        var customerA = await AddCustomerAsync(context, "agg-a@test.com", "Asha", "Perera");
        var customerB = await AddCustomerAsync(context, "agg-b@test.com", "Bimal", "Silva");
        var customerC = await AddCustomerAsync(context, "agg-c@test.com", "Chloe", "Fernando");

        var older = await AddReviewAsync(
            context,
            SeedData.ProductTShirt,
            customerA.CustomerId,
            rating: 5,
            comment: "Older review");
        var newer = await AddReviewAsync(
            context,
            SeedData.ProductTShirt,
            customerB.CustomerId,
            rating: 3,
            comment: "Newer review");
        await AddReviewAsync(
            context,
            SeedData.ProductTShirt,
            customerC.CustomerId,
            rating: 1,
            comment: "Hidden review",
            isPublished: false);

        await SetCreatedAtAsync(context, older, new DateTime(2026, 1, 1));
        await SetCreatedAtAsync(context, newer, new DateTime(2026, 6, 1));

        var service = new ReviewService(context);
        var response = await service.GetPublishedReviewsAsync(SeedData.ProductTShirt);

        Assert.Equal(SeedData.ProductTShirt, response.ProductId);
        Assert.Equal(2, response.Aggregate.TotalCount);
        Assert.Equal(4.0m, response.Aggregate.AverageRating);
        Assert.Equal(2, response.Reviews.Count);

        Assert.Equal("Newer review", response.Reviews[0].Comment);
        Assert.Equal("Older review", response.Reviews[1].Comment);

        var breakdown = response.Aggregate.Breakdown.Items
            .ToDictionary(item => item.Rating, item => item.Count);

        Assert.Equal(1, breakdown[5]);
        Assert.Equal(0, breakdown[4]);
        Assert.Equal(1, breakdown[3]);
        Assert.Equal(0, breakdown[2]);
        Assert.Equal(0, breakdown[1]);
    }

    [Fact]
    public async Task GetPublishedReviews_ExcludesUnpublishedReviews()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var context = await CreateContextAsync(connection);
        var customer = await AddCustomerAsync(context, "hidden@test.com", "Fay", "Rao");

        await AddReviewAsync(
            context,
            SeedData.ProductJeans,
            customer.CustomerId,
            rating: 4,
            comment: "Nice denim",
            isPublished: false);

        var service = new ReviewService(context);
        var response = await service.GetPublishedReviewsAsync(SeedData.ProductJeans);

        Assert.Empty(response.Reviews);
        Assert.Equal(0, response.Aggregate.TotalCount);
        Assert.Equal(0m, response.Aggregate.AverageRating);
    }

    [Fact]
    public async Task GetPublishedReviews_ReturnsEmptyAggregateForUnknownProduct()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var context = await CreateContextAsync(connection);
        var service = new ReviewService(context);

        var response = await service.GetPublishedReviewsAsync(Guid.NewGuid());

        Assert.Empty(response.Reviews);
        Assert.Equal(0, response.Aggregate.TotalCount);
        Assert.Equal(5, response.Aggregate.Breakdown.Items.Count);
    }

    [Fact]
    public async Task DeleteOwnReview_RemovesOnlyTheCustomersReview()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var context = await CreateContextAsync(connection);
        var owner = await AddCustomerAsync(context, "owner@test.com", "Gia", "Lopez");
        var other = await AddCustomerAsync(context, "other@test.com", "Hari", "Shah");

        await AddReviewAsync(context, SeedData.ProductJacket, other.CustomerId, 5);
        var controller = CreateController(new ReviewService(context), owner.UserId);

        var missing = await controller.DeleteOwnReview(
            SeedData.ProductJacket,
            CancellationToken.None);

        var notFound = Assert.IsType<ObjectResult>(missing);
        Assert.Equal(StatusCodes.Status404NotFound, notFound.StatusCode);
        Assert.Equal(1, await context.Reviews.CountAsync());

        await AddReviewAsync(context, SeedData.ProductJacket, owner.CustomerId, 2);
        var removed = await controller.DeleteOwnReview(
            SeedData.ProductJacket,
            CancellationToken.None);

        Assert.IsType<NoContentResult>(removed);
        var remaining = await context.Reviews.SingleAsync();
        Assert.Equal(other.CustomerId, remaining.CustomerId);
    }

    [Fact]
    public async Task GetOwnReview_ReturnsCustomersReviewOrNull()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var context = await CreateContextAsync(connection);
        var owner = await AddCustomerAsync(context, "mine@test.com", "Ivy", "Bose");
        var other = await AddCustomerAsync(context, "notmine@test.com", "Jai", "Menon");
        await AddReviewAsync(context, SeedData.ProductTShirt, other.CustomerId, 5);

        var service = new ReviewService(context);

        Assert.Null(await service.GetOwnReviewAsync(
            owner.UserId,
            SeedData.ProductTShirt));

        var mine = await service.GetOwnReviewAsync(
            owner.UserId,
            SeedData.ProductTShirt);

        Assert.Null(mine);

        await AddReviewAsync(context, SeedData.ProductTShirt, owner.CustomerId, 4);
        var own = await service.GetOwnReviewAsync(
            owner.UserId,
            SeedData.ProductTShirt);

        Assert.NotNull(own);
        Assert.Equal(4, own!.Rating);
    }

    [Fact]
    public async Task Moderation_HidesAndUnhidesReviews()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var context = await CreateContextAsync(connection);
        var customer = await AddCustomerAsync(context, "moderate@test.com", "Kate", "Iyer");
        var review = await AddReviewAsync(
            context,
            SeedData.ProductHoodie,
            customer.CustomerId,
            5,
            "Great hoodie");

        var service = new ReviewService(context);

        var hidden = await service.SetPublishedAsync(review.Id, false);
        Assert.NotNull(hidden);
        Assert.False(hidden!.IsPublished);

        var publicView = await service.GetPublishedReviewsAsync(SeedData.ProductHoodie);
        Assert.Empty(publicView.Reviews);

        var visible = await service.SetPublishedAsync(review.Id, true);
        Assert.NotNull(visible);
        Assert.True(visible!.IsPublished);

        publicView = await service.GetPublishedReviewsAsync(SeedData.ProductHoodie);
        Assert.Single(publicView.Reviews);

        Assert.Null(await service.SetPublishedAsync(Guid.NewGuid(), false));
    }

    [Fact]
    public async Task Moderation_ListsFilteredAndPagedReviews()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var context = await CreateContextAsync(connection);
        var customerA = await AddCustomerAsync(context, "list-a@test.com", "Lena", "Roy");
        var customerB = await AddCustomerAsync(context, "list-b@test.com", "Milo", "Diaz");
        var customerC = await AddCustomerAsync(context, "list-c@test.com", "Nina", "Khan");

        await AddReviewAsync(context, SeedData.ProductTShirt, customerA.CustomerId, 5);
        await AddReviewAsync(context, SeedData.ProductTShirt, customerB.CustomerId, 2);
        await AddReviewAsync(context, SeedData.ProductBoots, customerC.CustomerId, 4);
        await AddReviewAsync(
            context,
            SeedData.ProductBoots,
            customerA.CustomerId,
            3,
            isPublished: false);

        var service = new ReviewService(context);

        var byProduct = await service.GetReviewsForModerationAsync(
            new StaffReviewQuery { ProductId = SeedData.ProductBoots });
        Assert.Equal(2, byProduct.TotalItems);
        Assert.All(
            byProduct.Items,
            item => Assert.Equal(SeedData.ProductBoots, item.ProductId));

        var hidden = await service.GetReviewsForModerationAsync(
            new StaffReviewQuery { IsPublished = false });
        Assert.Equal(1, hidden.TotalItems);
        Assert.False(Assert.Single(hidden.Items).IsPublished);

        var minRating = await service.GetReviewsForModerationAsync(
            new StaffReviewQuery { MinRating = 4 });
        Assert.Equal(2, minRating.TotalItems);
        Assert.All(minRating.Items, item => Assert.True(item.Rating >= 4));

        var paged = await service.GetReviewsForModerationAsync(
            new StaffReviewQuery { Page = 1, PageSize = 2 });
        Assert.Equal(4, paged.TotalItems);
        Assert.Equal(2, paged.PageSize);
        Assert.Equal(2, paged.TotalPages);
        Assert.Equal(2, paged.Items.Count);
    }

    [Fact]
    public async Task Moderation_DeletesAnyReview()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var context = await CreateContextAsync(connection);
        var customer = await AddCustomerAsync(context, "delete@test.com", "Omar", "Nasser");
        var review = await AddReviewAsync(
            context,
            SeedData.ProductTShirt,
            customer.CustomerId,
            1);

        var service = new ReviewService(context);

        Assert.False(await service.DeleteReviewAsync(Guid.NewGuid()));
        Assert.True(await service.DeleteReviewAsync(review.Id));
        Assert.Empty(context.Reviews);
    }

    [Fact]
    public async Task SaveReview_UsesPrivacySafeDisplayName()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var context = await CreateContextAsync(connection);

        var withLast = await AddCustomerAsync(context, "name@test.com", "Priya", "Kumar");
        var firstNameOnly = await AddCustomerAsync(context, "single@test.com", "Solo", "");
        var service = new ReviewService(context);

        var first = await service.SaveReviewAsync(
            withLast.UserId,
            SeedData.ProductTShirt,
            new SaveReviewRequest { Rating = 5 });
        var second = await service.SaveReviewAsync(
            firstNameOnly.UserId,
            SeedData.ProductHoodie,
            new SaveReviewRequest { Rating = 4 });

        Assert.Equal("Priya K.", first.Review!.DisplayName);
        Assert.Equal("Solo", second.Review!.DisplayName);
        Assert.DoesNotContain("name@test.com", first.Review.DisplayName);
    }

    private static async Task<AppDbContext> CreateContextAsync(
        SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;
        var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();
        return context;
    }

    private static async Task<(int UserId, int CustomerId)> AddCustomerAsync(
        AppDbContext context,
        string email,
        string firstName,
        string lastName)
    {
        var customer = new Customer
        {
            User = new User
            {
                Email = email,
                PasswordHash = "test-only-password-hash",
                FirstName = firstName,
                LastName = lastName,
                RoleId = 1,
                IsActive = true
            }
        };

        context.Customers.Add(customer);
        await context.SaveChangesAsync();
        return (customer.UserId, customer.Id);
    }

    private static async Task<Review> AddReviewAsync(
        AppDbContext context,
        Guid productId,
        int customerId,
        int rating,
        string? comment = null,
        bool isPublished = true)
    {
        var review = new Review
        {
            ProductId = productId,
            CustomerId = customerId,
            Rating = rating,
            Comment = comment,
            IsPublished = isPublished
        };

        context.Reviews.Add(review);
        await context.SaveChangesAsync();
        return review;
    }

    private static async Task SetCreatedAtAsync(
        AppDbContext context,
        Review review,
        DateTime createdAt)
    {
        review.CreatedAt = DateTime.SpecifyKind(createdAt, DateTimeKind.Utc);
        await context.SaveChangesAsync();
    }

    private static ReviewsController CreateController(
        IReviewService service,
        int? userId = null)
    {
        var claims = userId.HasValue
            ? new[]
            {
                new Claim(
                    ClaimTypes.NameIdentifier,
                    userId.Value.ToString())
            }
            : Array.Empty<Claim>();
        var identity = new ClaimsIdentity(
            claims,
            userId.HasValue ? "TestAuthentication" : null);
        var controller = new ReviewsController(service)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(identity)
                }
            }
        };

        return controller;
    }
}
