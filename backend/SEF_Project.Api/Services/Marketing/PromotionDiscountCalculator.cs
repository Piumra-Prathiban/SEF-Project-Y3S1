using SEF_Project.Api.Models.Enums;
using SEF_Project.Api.Models.Marketing;

namespace SEF_Project.Api.Services.Marketing;

public readonly record struct PromotionDiscount(
    decimal OriginalPrice,
    decimal DiscountAmount,
    decimal FinalPrice);

/// <summary>
/// Pure, deterministic promotion pricing rules (no database access), so every
/// rule can be unit-tested in isolation. Business-rule violations throw
/// <see cref="InvalidOperationException"/> (mapped to 409 Conflict).
/// </summary>
public static class PromotionDiscountCalculator
{
    public const decimal MaxPercentage = 100m;

    public static PromotionDiscount Calculate(
        Promotion promotion,
        decimal unitPrice,
        DateTime nowUtc)
    {
        EnsureValidConfiguration(promotion);
        EnsureActive(promotion);
        EnsureWithinDates(promotion, nowUtc);
        EnsureValidPrice(unitPrice);

        var originalPrice = RoundMoney(unitPrice);

        var discount = promotion.Type switch
        {
            PromotionType.PercentageDiscount =>
                RoundMoney(originalPrice * promotion.DiscountValue / 100m),
            PromotionType.FixedAmountDiscount =>
                RoundMoney(promotion.DiscountValue),
            _ => throw new InvalidOperationException(
                $"Promotion type '{promotion.Type}' does not discount item prices.")
        };

        // A discount can never exceed the price it applies to.
        discount = Math.Min(discount, originalPrice);

        return new PromotionDiscount(
            originalPrice,
            discount,
            originalPrice - discount);
    }

    public static void EnsureValidConfiguration(Promotion promotion)
    {
        if (promotion.EndDate < promotion.StartDate)
        {
            throw new InvalidOperationException(
                "Promotion configuration is invalid: end date is before start date.");
        }

        if (promotion.DiscountValue < 0)
        {
            throw new InvalidOperationException(
                "Promotion configuration is invalid: discount cannot be negative.");
        }

        if (promotion.Type == PromotionType.PercentageDiscount
            && (promotion.DiscountValue <= 0 || promotion.DiscountValue > MaxPercentage))
        {
            throw new InvalidOperationException(
                "Promotion configuration is invalid: percentage must be greater than 0 and at most 100.");
        }

        if (promotion.Type == PromotionType.FixedAmountDiscount
            && promotion.DiscountValue <= 0)
        {
            throw new InvalidOperationException(
                "Promotion configuration is invalid: fixed discount must be greater than zero.");
        }
    }

    public static void EnsureActive(Promotion promotion)
    {
        if (!promotion.IsActive)
        {
            throw new InvalidOperationException("Promotion is not active.");
        }
    }

    // StartDate and EndDate are both inclusive instants (UTC).
    public static void EnsureWithinDates(Promotion promotion, DateTime nowUtc)
    {
        if (nowUtc < promotion.StartDate)
        {
            throw new InvalidOperationException("Promotion has not started yet.");
        }

        if (nowUtc > promotion.EndDate)
        {
            throw new InvalidOperationException("Promotion has expired.");
        }
    }

    public static void EnsureValidPrice(decimal unitPrice)
    {
        if (unitPrice <= 0)
        {
            throw new InvalidOperationException(
                "Product variant has an invalid price.");
        }
    }

    // Money is rounded half away from zero to 2 decimal places.
    private static decimal RoundMoney(decimal value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
