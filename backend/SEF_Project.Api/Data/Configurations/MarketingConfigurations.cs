using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SEF_Project.Api.Models.Enums;
using SEF_Project.Api.Models.Marketing;

namespace SEF_Project.Api.Data.Configurations;

internal static class MarketingConstraintSql
{
    // Builds "'A', 'B', 'C'" from an enum so CHECK constraints stay in sync
    // with enums that are stored as strings.
    public static string EnumValues<TEnum>() where TEnum : struct, Enum =>
        string.Join(", ", Enum.GetNames<TEnum>().Select(n => $"'{n}'"));
}

public class CampaignConfiguration : IEntityTypeConfiguration<Campaign>
{
    public void Configure(EntityTypeBuilder<Campaign> builder)
    {
        builder.ToTable("Campaigns", table =>
        {
            table.HasCheckConstraint(
                "CK_Campaigns_DateRange",
                "\"EndDate\" >= \"StartDate\"");
            table.HasCheckConstraint(
                "CK_Campaigns_Status",
                $"\"Status\" IN ({MarketingConstraintSql.EnumValues<CampaignStatus>()})");
        });

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Name).IsRequired().HasMaxLength(200);
        builder.Property(e => e.Description).HasMaxLength(2000);
        builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(50);

        builder.HasIndex(e => e.Status);
        builder.HasIndex(e => new { e.StartDate, e.EndDate });

        builder.HasData(
            new Campaign
            {
                Id = SeedData.CampaignSummer,
                Name = "Summer Launch",
                Description = "Launch promotion for the new menu.",
                StartDate = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc),
                EndDate = new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc),
                Status = CampaignStatus.Active
            },
            new Campaign
            {
                Id = SeedData.CampaignWeekendRefresh,
                Name = "Weekend Refresh",
                Description = "Weekend beverage deals.",
                StartDate = new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                EndDate = new DateTime(2027, 3, 31, 0, 0, 0, DateTimeKind.Utc),
                Status = CampaignStatus.Scheduled
            });
    }
}

public class PromotionConfiguration : IEntityTypeConfiguration<Promotion>
{
    public void Configure(EntityTypeBuilder<Promotion> builder)
    {
        builder.ToTable("Promotions", table =>
        {
            // Percentage discounts are (0, 100]; fixed-amount discounts are > 0;
            // other types (e.g. FreeShipping) may carry a zero value.
            // The CAST keeps the check numeric on SQLite (used by tests), which
            // stores decimals as TEXT; it is a no-op comparison on PostgreSQL.
            const string discount = "CAST(\"DiscountValue\" AS REAL)";
            table.HasCheckConstraint(
                "CK_Promotions_DiscountValue",
                $"{discount} >= 0"
                + $" AND (\"Type\" <> 'PercentageDiscount' OR ({discount} > 0 AND {discount} <= 100))"
                + $" AND (\"Type\" <> 'FixedAmountDiscount' OR {discount} > 0)");
            table.HasCheckConstraint(
                "CK_Promotions_DateRange",
                "\"EndDate\" >= \"StartDate\"");
            table.HasCheckConstraint(
                "CK_Promotions_Type",
                $"\"Type\" IN ({MarketingConstraintSql.EnumValues<PromotionType>()})");
        });

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Name).IsRequired().HasMaxLength(200);
        builder.Property(e => e.Description).HasMaxLength(2000);
        builder.Property(e => e.Type).HasConversion<string>().HasMaxLength(50);
        builder.Property(e => e.DiscountValue).HasPrecision(18, 2);

        builder.HasIndex(e => new { e.IsActive, e.StartDate, e.EndDate });

        builder.HasOne(e => e.Campaign)
            .WithMany(e => e.Promotions)
            .HasForeignKey(e => e.CampaignId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasData(
            new Promotion
            {
                Id = SeedData.PromotionPizza20,
                CampaignId = SeedData.CampaignSummer,
                Name = "Pizza 20% Off",
                Description = "20% off all pizzas.",
                Type = PromotionType.PercentageDiscount,
                DiscountValue = 20m,
                StartDate = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc),
                EndDate = new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc),
                IsActive = true
            },
            new Promotion
            {
                Id = SeedData.PromotionDessert10,
                CampaignId = SeedData.CampaignSummer,
                Name = "Dessert Week 10% Off",
                Description = "10% off every dessert.",
                Type = PromotionType.PercentageDiscount,
                DiscountValue = 10m,
                StartDate = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc),
                EndDate = new DateTime(2026, 10, 31, 0, 0, 0, DateTimeKind.Utc),
                IsActive = true
            },
            new Promotion
            {
                Id = SeedData.PromotionColaFixed,
                CampaignId = SeedData.CampaignWeekendRefresh,
                Name = "Cola Rs. 50 Off",
                Description = "Rs. 50 off each cola.",
                Type = PromotionType.FixedAmountDiscount,
                DiscountValue = 50m,
                StartDate = new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                EndDate = new DateTime(2027, 3, 31, 0, 0, 0, DateTimeKind.Utc),
                IsActive = true
            },
            new Promotion
            {
                Id = SeedData.PromotionFreeDelivery,
                Name = "Free Delivery",
                Description = "Free delivery with a coupon code.",
                Type = PromotionType.FreeShipping,
                DiscountValue = 0m,
                StartDate = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc),
                EndDate = new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc),
                IsActive = true
            });
    }
}

public class PromotionProductConfiguration : IEntityTypeConfiguration<PromotionProduct>
{
    public void Configure(EntityTypeBuilder<PromotionProduct> builder)
    {
        builder.ToTable("PromotionProducts");

        builder.HasKey(e => new { e.PromotionId, e.ProductId });

        builder.HasOne(e => e.Promotion)
            .WithMany(e => e.PromotionProducts)
            .HasForeignKey(e => e.PromotionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Product)
            .WithMany()
            .HasForeignKey(e => e.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasData(
            new PromotionProduct
            {
                PromotionId = SeedData.PromotionPizza20,
                ProductId = SeedData.ProductMargherita
            },
            new PromotionProduct
            {
                PromotionId = SeedData.PromotionPizza20,
                ProductId = SeedData.ProductPepperoni
            },
            new PromotionProduct
            {
                PromotionId = SeedData.PromotionColaFixed,
                ProductId = SeedData.ProductCola
            });
    }
}

public class PromotionCategoryConfiguration : IEntityTypeConfiguration<PromotionCategory>
{
    public void Configure(EntityTypeBuilder<PromotionCategory> builder)
    {
        builder.ToTable("PromotionCategories");

        builder.HasKey(e => new { e.PromotionId, e.CategoryId });

        builder.HasOne(e => e.Promotion)
            .WithMany(e => e.PromotionCategories)
            .HasForeignKey(e => e.PromotionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Category)
            .WithMany()
            .HasForeignKey(e => e.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasData(
            new PromotionCategory
            {
                PromotionId = SeedData.PromotionDessert10,
                CategoryId = SeedData.CategoryDesserts
            });
    }
}

public class CouponConfiguration : IEntityTypeConfiguration<Coupon>
{
    public void Configure(EntityTypeBuilder<Coupon> builder)
    {
        builder.ToTable("Coupons", table =>
        {
            table.HasCheckConstraint(
                "CK_Coupons_UsageLimit",
                "\"UsageLimit\" IS NULL OR \"UsageLimit\" > 0");
            table.HasCheckConstraint(
                "CK_Coupons_PerCustomerLimit",
                "\"PerCustomerLimit\" IS NULL OR \"PerCustomerLimit\" > 0");
            table.HasCheckConstraint(
                "CK_Coupons_DateRange",
                "\"EndsAt\" >= \"StartsAt\"");
        });

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Code).IsRequired().HasMaxLength(50);

        builder.HasIndex(e => e.Code).IsUnique();

        builder.HasOne(e => e.Promotion)
            .WithMany(e => e.Coupons)
            .HasForeignKey(e => e.PromotionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasData(
            new Coupon
            {
                Id = SeedData.CouponSummer20,
                Code = "SUMMER20",
                PromotionId = SeedData.PromotionPizza20,
                UsageLimit = 100,
                PerCustomerLimit = 1,
                StartsAt = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc),
                EndsAt = new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc),
                IsActive = true
            },
            new Coupon
            {
                Id = SeedData.CouponFreeDelivery,
                Code = "FREEDELIVERY",
                PromotionId = SeedData.PromotionFreeDelivery,
                UsageLimit = 500,
                PerCustomerLimit = 3,
                StartsAt = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc),
                EndsAt = new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc),
                IsActive = true
            });
    }
}

public class CouponRedemptionConfiguration : IEntityTypeConfiguration<CouponRedemption>
{
    public void Configure(EntityTypeBuilder<CouponRedemption> builder)
    {
        builder.ToTable("CouponRedemptions");

        builder.HasKey(e => e.Id);

        builder.HasIndex(e => e.CustomerId);
        builder.HasIndex(e => e.RedeemedAt);

        // A coupon can be redeemed at most once per order (NULL OrderIds are
        // distinct, so redemptions without an order are not affected). Also
        // serves CouponId lookups for usage-limit checks.
        builder.HasIndex(e => new { e.CouponId, e.OrderId }).IsUnique();

        builder.HasOne(e => e.Coupon)
            .WithMany(e => e.Redemptions)
            .HasForeignKey(e => e.CouponId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Customer)
            .WithMany()
            .HasForeignKey(e => e.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Order)
            .WithMany()
            .HasForeignKey(e => e.OrderId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
