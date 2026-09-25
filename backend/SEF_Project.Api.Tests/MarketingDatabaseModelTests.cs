using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using SEF_Project.Api.Data;
using SEF_Project.Api.Data.Configurations;
using SEF_Project.Api.Models.Catalog;
using SEF_Project.Api.Models.Enums;
using SEF_Project.Api.Models.Marketing;

namespace SEF_Project.Api.Tests;

public class MarketingDatabaseModelTests
{
    private static IModel BuildModel()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=sef_project_db")
            .Options;

        using var context = new AppDbContext(options);
        return context.Model;
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

    private static Promotion ValidPromotion() =>
        new()
        {
            Name = "Test Promotion",
            Type = PromotionType.PercentageDiscount,
            DiscountValue = 15m,
            StartDate = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc),
            EndDate = new DateTime(2026, 9, 30, 0, 0, 0, DateTimeKind.Utc),
            IsActive = true
        };

    [Fact]
    public void Campaign_Status_ShouldBeStoredAsString()
    {
        var model = BuildModel();

        var status = model.FindEntityType(typeof(Campaign))!
            .FindProperty(nameof(Campaign.Status))!;

        Assert.Equal(typeof(string), status.GetProviderClrType());
        Assert.Equal(50, status.GetMaxLength());
    }

    [Fact]
    public void Promotion_Campaign_ShouldBeOptional_WithRestrictDelete()
    {
        var model = BuildModel();

        var fk = model.FindEntityType(typeof(Promotion))!
            .GetForeignKeys()
            .Single(f => f.PrincipalEntityType.ClrType == typeof(Campaign));

        Assert.False(fk.IsRequired);
        Assert.Equal(DeleteBehavior.Restrict, fk.DeleteBehavior);
    }

    [Fact]
    public void PromotionProduct_ShouldUseCompositeKey_AndReferenceProduct()
    {
        var model = BuildModel();

        var entity = model.FindEntityType(typeof(PromotionProduct))!;
        var keyProperties = entity.FindPrimaryKey()!.Properties
            .Select(p => p.Name)
            .ToArray();

        Assert.Equal(
            new[] { nameof(PromotionProduct.PromotionId), nameof(PromotionProduct.ProductId) },
            keyProperties);

        var productFk = entity.GetForeignKeys()
            .Single(f => f.Properties.Any(p => p.Name == nameof(PromotionProduct.ProductId)));

        Assert.Equal(typeof(Product), productFk.PrincipalEntityType.ClrType);
        Assert.Equal(DeleteBehavior.Restrict, productFk.DeleteBehavior);
    }

    [Fact]
    public void CouponRedemption_CouponAndOrder_ShouldBeUnique()
    {
        var model = BuildModel();

        var index = model.FindEntityType(typeof(CouponRedemption))!
            .GetIndexes()
            .Single(i => i.Properties.Select(p => p.Name).SequenceEqual(
                new[] { nameof(CouponRedemption.CouponId), nameof(CouponRedemption.OrderId) }));

        Assert.True(index.IsUnique);
    }

    [Fact]
    public void Promotion_DiscountValue_ShouldUse_DecimalPrecision18_2()
    {
        var model = BuildModel();

        var discount = model.FindEntityType(typeof(Promotion))!
            .FindProperty(nameof(Promotion.DiscountValue))!;

        Assert.Equal(18, discount.GetPrecision());
        Assert.Equal(2, discount.GetScale());
    }

    [Fact]
    public async Task SeedData_ShouldContain_MarketingCampaignsPromotionsAndCoupons()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;

        var summer = await context.Campaigns
            .Include(c => c.Promotions)
            .SingleAsync(c => c.Id == SeedData.CampaignSummer);

        Assert.Equal(CampaignStatus.Active, summer.Status);
        Assert.Equal(2, summer.Promotions.Count);

        var footwear = await context.Promotions
            .Include(p => p.PromotionCategories)
            .SingleAsync(p => p.Id == SeedData.PromotionFootwear10);

        Assert.Contains(
            footwear.PromotionCategories,
            pc => pc.CategoryId == SeedData.CategoryFootwear);

        var freeDelivery = await context.Promotions
            .SingleAsync(p => p.Id == SeedData.PromotionFreeDelivery);

        Assert.Null(freeDelivery.CampaignId);
        Assert.Equal(PromotionType.FreeShipping, freeDelivery.Type);

        Assert.Equal(2, await context.Coupons.CountAsync());
    }

    [Theory]
    [InlineData(PromotionType.PercentageDiscount, 150)]
    [InlineData(PromotionType.PercentageDiscount, 0)]
    [InlineData(PromotionType.FixedAmountDiscount, 0)]
    [InlineData(PromotionType.FreeShipping, -1)]
    public async Task Promotion_ShouldBeRejected_WhenDiscountValueIsInvalid(
        PromotionType type,
        int discountValue)
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;

        var promotion = ValidPromotion();
        promotion.Type = type;
        promotion.DiscountValue = discountValue;

        context.Promotions.Add(promotion);

        await Assert.ThrowsAsync<DbUpdateException>(
            () => context.SaveChangesAsync());
    }

    [Fact]
    public async Task Promotion_ShouldBeRejected_WhenEndDateIsBeforeStartDate()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;

        var promotion = ValidPromotion();
        promotion.EndDate = promotion.StartDate.AddDays(-1);

        context.Promotions.Add(promotion);

        await Assert.ThrowsAsync<DbUpdateException>(
            () => context.SaveChangesAsync());
    }

    [Fact]
    public async Task Promotion_ShouldBeSaved_WhenValuesAreValid()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;

        context.Promotions.Add(ValidPromotion());
        await context.SaveChangesAsync();

        Assert.Equal(5, await context.Promotions.CountAsync());
    }

    [Fact]
    public async Task Campaign_ShouldBeRejected_WhenEndDateIsBeforeStartDate()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;

        context.Campaigns.Add(new Campaign
        {
            Name = "Broken Campaign",
            StartDate = new DateTime(2026, 9, 30, 0, 0, 0, DateTimeKind.Utc),
            EndDate = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc)
        });

        await Assert.ThrowsAsync<DbUpdateException>(
            () => context.SaveChangesAsync());
    }

    [Fact]
    public async Task Coupon_ShouldBeRejected_WhenUsageLimitIsNotPositive()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;

        context.Coupons.Add(new Coupon
        {
            Code = "ZERO",
            UsageLimit = 0,
            StartsAt = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc),
            EndsAt = new DateTime(2026, 9, 30, 0, 0, 0, DateTimeKind.Utc)
        });

        await Assert.ThrowsAsync<DbUpdateException>(
            () => context.SaveChangesAsync());
    }
}
