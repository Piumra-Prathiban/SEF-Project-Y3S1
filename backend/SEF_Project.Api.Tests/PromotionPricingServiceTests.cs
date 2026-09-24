using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SEF_Project.Api.Data;
using SEF_Project.Api.Data.Configurations;
using SEF_Project.Api.DTOs.Marketing;
using SEF_Project.Api.Models.Enums;
using SEF_Project.Api.Services.Marketing;

namespace SEF_Project.Api.Tests;

public class PromotionPricingServiceTests
{
    // Inside the seeded "Pizza 20% Off" and "Dessert Week 10% Off" windows.
    private static readonly DateTime October2026 =
        new(2026, 10, 15, 12, 0, 0, DateTimeKind.Utc);

    // Inside the seeded "Cola Rs. 50 Off" window.
    private static readonly DateTime February2027 =
        new(2027, 2, 1, 12, 0, 0, DateTimeKind.Utc);

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _now;

        public FixedTimeProvider(DateTime utcNow)
        {
            _now = new DateTimeOffset(utcNow, TimeSpan.Zero);
        }

        public override DateTimeOffset GetUtcNow() => _now;
    }

    private static async Task<(SqliteConnection Connection, AppDbContext Context)>
        CreateContextAsync()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();

        return (connection, context);
    }

    private static PromotionPricingService CreateService(
        AppDbContext context,
        DateTime utcNow) =>
        new(
            context,
            new FixedTimeProvider(utcNow),
            NullLogger<PromotionPricingService>.Instance);

    private static CalculatePromotionDiscountRequest Request(Guid variantId) =>
        new() { ProductVariantId = variantId };

    [Fact]
    public async Task CalculatePromotionDiscountAsync_ShouldApplyPercentage_WhenProductIsTargeted()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;

        var result = await CreateService(context, October2026)
            .CalculatePromotionDiscountAsync(
                SeedData.PromotionPizza20,
                Request(SeedData.VariantMargheritaSmall));

        Assert.NotNull(result);
        Assert.Equal(SeedData.PromotionPizza20, result.PromotionId);
        Assert.Equal(SeedData.VariantMargheritaSmall, result.ProductVariantId);
        Assert.Equal(SeedData.ProductMargherita, result.ProductId);
        Assert.Equal("PIZ-MARG-S", result.Sku);
        Assert.Equal(1200m, result.OriginalPrice);
        Assert.Equal(240m, result.DiscountAmount);
        Assert.Equal(960m, result.FinalPrice);
        Assert.Equal("LKR", result.Currency);
        Assert.Equal(October2026, result.CalculatedAt);
    }

    [Fact]
    public async Task CalculatePromotionDiscountAsync_ShouldApplyPercentage_WhenCategoryIsTargeted()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;

        var result = await CreateService(context, October2026)
            .CalculatePromotionDiscountAsync(
                SeedData.PromotionDessert10,
                Request(SeedData.VariantTiramisuSingle));

        Assert.NotNull(result);
        Assert.Equal(900m, result.OriginalPrice);
        Assert.Equal(90m, result.DiscountAmount);
        Assert.Equal(810m, result.FinalPrice);
    }

    [Fact]
    public async Task CalculatePromotionDiscountAsync_ShouldApplyFixedAmount_WhenCampaignIsActive()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;

        var campaign = await context.Campaigns
            .SingleAsync(c => c.Id == SeedData.CampaignWeekendRefresh);
        campaign.Status = CampaignStatus.Active;
        await context.SaveChangesAsync();

        var result = await CreateService(context, February2027)
            .CalculatePromotionDiscountAsync(
                SeedData.PromotionColaFixed,
                Request(SeedData.VariantCola330));

        Assert.NotNull(result);
        Assert.Equal(300m, result.OriginalPrice);
        Assert.Equal(50m, result.DiscountAmount);
        Assert.Equal(250m, result.FinalPrice);
    }

    [Fact]
    public async Task CalculatePromotionDiscountAsync_ShouldReject_WhenCampaignIsNotActive()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;

        // Seeded "Weekend Refresh" campaign is Scheduled.
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateService(context, February2027)
                .CalculatePromotionDiscountAsync(
                    SeedData.PromotionColaFixed,
                    Request(SeedData.VariantCola330)));

        Assert.Contains("campaign is not active", ex.Message);
    }

    [Fact]
    public async Task CalculatePromotionDiscountAsync_ShouldReject_WhenProductIsNotIncluded()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateService(context, October2026)
                .CalculatePromotionDiscountAsync(
                    SeedData.PromotionPizza20,
                    Request(SeedData.VariantCarbonaraRegular)));

        Assert.Contains("does not apply", ex.Message);
    }

    [Fact]
    public async Task CalculatePromotionDiscountAsync_ShouldReject_WhenPromotionHasExpired()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateService(context, new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc))
                .CalculatePromotionDiscountAsync(
                    SeedData.PromotionPizza20,
                    Request(SeedData.VariantMargheritaSmall)));

        Assert.Equal("Promotion has expired.", ex.Message);
    }

    [Fact]
    public async Task CalculatePromotionDiscountAsync_ShouldReject_WhenPromotionIsInTheFuture()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateService(context, new DateTime(2026, 8, 31, 23, 59, 59, DateTimeKind.Utc))
                .CalculatePromotionDiscountAsync(
                    SeedData.PromotionPizza20,
                    Request(SeedData.VariantMargheritaSmall)));

        Assert.Equal("Promotion has not started yet.", ex.Message);
    }

    [Fact]
    public async Task CalculatePromotionDiscountAsync_ShouldReject_WhenPromotionIsInactive()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;

        var promotion = await context.Promotions
            .SingleAsync(p => p.Id == SeedData.PromotionPizza20);
        promotion.IsActive = false;
        await context.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateService(context, October2026)
                .CalculatePromotionDiscountAsync(
                    SeedData.PromotionPizza20,
                    Request(SeedData.VariantMargheritaSmall)));

        Assert.Equal("Promotion is not active.", ex.Message);
    }

    [Fact]
    public async Task CalculatePromotionDiscountAsync_ShouldReject_WhenStoredPriceIsInvalid()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;

        var variant = await context.ProductVariants
            .SingleAsync(v => v.Id == SeedData.VariantMargheritaSmall);
        variant.Price = 0m;
        await context.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateService(context, October2026)
                .CalculatePromotionDiscountAsync(
                    SeedData.PromotionPizza20,
                    Request(SeedData.VariantMargheritaSmall)));

        Assert.Equal("Product variant has an invalid price.", ex.Message);
    }

    [Fact]
    public async Task CalculatePromotionDiscountAsync_ShouldReject_WhenVariantIsInactive()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;

        var variant = await context.ProductVariants
            .SingleAsync(v => v.Id == SeedData.VariantMargheritaSmall);
        variant.IsActive = false;
        await context.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateService(context, October2026)
                .CalculatePromotionDiscountAsync(
                    SeedData.PromotionPizza20,
                    Request(SeedData.VariantMargheritaSmall)));

        Assert.Contains("is not available", ex.Message);
    }

    [Fact]
    public async Task CalculatePromotionDiscountAsync_ShouldReturnNull_WhenPromotionDoesNotExist()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;

        var result = await CreateService(context, October2026)
            .CalculatePromotionDiscountAsync(
                Guid.NewGuid(),
                Request(SeedData.VariantMargheritaSmall));

        Assert.Null(result);
    }

    [Fact]
    public async Task CalculatePromotionDiscountAsync_ShouldReturnNull_WhenVariantDoesNotExist()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;

        var result = await CreateService(context, October2026)
            .CalculatePromotionDiscountAsync(
                SeedData.PromotionPizza20,
                Request(Guid.NewGuid()));

        Assert.Null(result);
    }

    [Theory]
    [InlineData(2026, 9, 1)]    // exactly StartDate
    [InlineData(2026, 12, 31)]  // exactly EndDate
    public async Task CalculatePromotionDiscountAsync_ShouldApply_WhenNowIsOnBoundaryDate(
        int year,
        int month,
        int day)
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;

        var result = await CreateService(
                context,
                new DateTime(year, month, day, 0, 0, 0, DateTimeKind.Utc))
            .CalculatePromotionDiscountAsync(
                SeedData.PromotionPizza20,
                Request(SeedData.VariantPepperoniLarge));

        Assert.NotNull(result);
        Assert.Equal(2600m, result.OriginalPrice);
        Assert.Equal(520m, result.DiscountAmount);
        Assert.Equal(2080m, result.FinalPrice);
    }
}
