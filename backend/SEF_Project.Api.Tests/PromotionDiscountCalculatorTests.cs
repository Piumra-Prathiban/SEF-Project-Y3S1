using SEF_Project.Api.Models.Enums;
using SEF_Project.Api.Models.Marketing;
using SEF_Project.Api.Services.Marketing;

namespace SEF_Project.Api.Tests;

public class PromotionDiscountCalculatorTests
{
    private static readonly DateTime Start =
        new(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);

    private static readonly DateTime End =
        new(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc);

    private static readonly DateTime Now =
        new(2026, 10, 15, 12, 0, 0, DateTimeKind.Utc);

    private static Promotion CreatePromotion(
        PromotionType type = PromotionType.PercentageDiscount,
        decimal discountValue = 20m,
        bool isActive = true) =>
        new()
        {
            Id = Guid.NewGuid(),
            Name = "Test Promotion",
            Type = type,
            DiscountValue = discountValue,
            StartDate = Start,
            EndDate = End,
            IsActive = isActive
        };

    [Fact]
    public void Calculate_ShouldApplyPercentage_WhenPromotionIsValid()
    {
        var result = PromotionDiscountCalculator.Calculate(
            CreatePromotion(PromotionType.PercentageDiscount, 20m),
            10000m,
            Now);

        Assert.Equal(10000m, result.OriginalPrice);
        Assert.Equal(2000m, result.DiscountAmount);
        Assert.Equal(8000m, result.FinalPrice);
    }

    [Fact]
    public void Calculate_ShouldApplyFixedAmount_WhenPromotionIsValid()
    {
        var result = PromotionDiscountCalculator.Calculate(
            CreatePromotion(PromotionType.FixedAmountDiscount, 1500m),
            10000m,
            Now);

        Assert.Equal(10000m, result.OriginalPrice);
        Assert.Equal(1500m, result.DiscountAmount);
        Assert.Equal(8500m, result.FinalPrice);
    }

    [Fact]
    public void Calculate_ShouldCapDiscountAtPrice_WhenFixedAmountExceedsPrice()
    {
        var result = PromotionDiscountCalculator.Calculate(
            CreatePromotion(PromotionType.FixedAmountDiscount, 500m),
            300m,
            Now);

        Assert.Equal(300m, result.DiscountAmount);
        Assert.Equal(0m, result.FinalPrice);
    }

    [Fact]
    public void Calculate_ShouldMakeItemFree_WhenPercentageIsOneHundred()
    {
        var result = PromotionDiscountCalculator.Calculate(
            CreatePromotion(PromotionType.PercentageDiscount, 100m),
            1200m,
            Now);

        Assert.Equal(1200m, result.DiscountAmount);
        Assert.Equal(0m, result.FinalPrice);
    }

    [Theory]
    [InlineData(999.99, 15, 150.00, 849.99)]   // 149.9985 rounds half away from zero
    [InlineData(1234.56, 12.5, 154.32, 1080.24)]
    [InlineData(0.05, 50, 0.03, 0.02)]         // 0.025 rounds up to 0.03
    public void Calculate_ShouldRoundToTwoDecimals_WhenPercentageProducesFractions(
        decimal price,
        decimal percentage,
        decimal expectedDiscount,
        decimal expectedFinal)
    {
        var result = PromotionDiscountCalculator.Calculate(
            CreatePromotion(PromotionType.PercentageDiscount, percentage),
            price,
            Now);

        Assert.Equal(expectedDiscount, result.DiscountAmount);
        Assert.Equal(expectedFinal, result.FinalPrice);
        Assert.Equal(result.OriginalPrice, result.DiscountAmount + result.FinalPrice);
    }

    [Fact]
    public void Calculate_ShouldReject_WhenPromotionIsInactive()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            PromotionDiscountCalculator.Calculate(
                CreatePromotion(isActive: false),
                1000m,
                Now));

        Assert.Equal("Promotion is not active.", ex.Message);
    }

    [Fact]
    public void Calculate_ShouldReject_WhenPromotionHasExpired()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            PromotionDiscountCalculator.Calculate(
                CreatePromotion(),
                1000m,
                End.AddTicks(1)));

        Assert.Equal("Promotion has expired.", ex.Message);
    }

    [Fact]
    public void Calculate_ShouldReject_WhenPromotionIsInTheFuture()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            PromotionDiscountCalculator.Calculate(
                CreatePromotion(),
                1000m,
                Start.AddTicks(-1)));

        Assert.Equal("Promotion has not started yet.", ex.Message);
    }

    [Fact]
    public void Calculate_ShouldApply_WhenNowIsExactlyStartDate()
    {
        var result = PromotionDiscountCalculator.Calculate(
            CreatePromotion(),
            1000m,
            Start);

        Assert.Equal(800m, result.FinalPrice);
    }

    [Fact]
    public void Calculate_ShouldApply_WhenNowIsExactlyEndDate()
    {
        var result = PromotionDiscountCalculator.Calculate(
            CreatePromotion(),
            1000m,
            End);

        Assert.Equal(800m, result.FinalPrice);
    }

    [Theory]
    [InlineData(PromotionType.PercentageDiscount, 0)]
    [InlineData(PromotionType.PercentageDiscount, 100.01)]
    [InlineData(PromotionType.PercentageDiscount, -5)]
    [InlineData(PromotionType.FixedAmountDiscount, 0)]
    [InlineData(PromotionType.FixedAmountDiscount, -100)]
    public void Calculate_ShouldReject_WhenDiscountValueIsInvalid(
        PromotionType type,
        decimal discountValue)
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            PromotionDiscountCalculator.Calculate(
                CreatePromotion(type, discountValue),
                1000m,
                Now));

        Assert.StartsWith("Promotion configuration is invalid", ex.Message);
    }

    [Fact]
    public void Calculate_ShouldReject_WhenEndDateIsBeforeStartDate()
    {
        var promotion = CreatePromotion();
        promotion.EndDate = promotion.StartDate.AddDays(-1);

        var ex = Assert.Throws<InvalidOperationException>(() =>
            PromotionDiscountCalculator.Calculate(promotion, 1000m, Now));

        Assert.StartsWith("Promotion configuration is invalid", ex.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-1200)]
    public void Calculate_ShouldReject_WhenPriceIsInvalid(decimal price)
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            PromotionDiscountCalculator.Calculate(
                CreatePromotion(),
                price,
                Now));

        Assert.Equal("Product variant has an invalid price.", ex.Message);
    }

    [Theory]
    [InlineData(PromotionType.FreeShipping, 0)]
    [InlineData(PromotionType.BuyXGetY, 1)]
    public void Calculate_ShouldReject_WhenPromotionTypeDoesNotDiscountItems(
        PromotionType type,
        decimal discountValue)
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            PromotionDiscountCalculator.Calculate(
                CreatePromotion(type, discountValue),
                1000m,
                Now));

        Assert.Contains("does not discount item prices", ex.Message);
    }
}
