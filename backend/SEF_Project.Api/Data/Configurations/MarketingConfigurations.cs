using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SEF_Project.Api.Models.Enums;
using SEF_Project.Api.Models.Marketing;

namespace SEF_Project.Api.Data.Configurations;

public class CampaignConfiguration : IEntityTypeConfiguration<Campaign>
{
    public void Configure(EntityTypeBuilder<Campaign> builder)
    {
        builder.ToTable("Campaigns");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Name).IsRequired().HasMaxLength(200);
        builder.Property(e => e.Description).HasMaxLength(2000);

        builder.HasData(
            new Campaign
            {
                Id = SeedData.CampaignSummer,
                Name = "Summer Launch",
                Description = "Launch promotion for the new season.",
                StartDate = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc),
                EndDate = new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc),
                IsActive = true
            });
    }
}

public class PromotionConfiguration : IEntityTypeConfiguration<Promotion>
{
    public void Configure(EntityTypeBuilder<Promotion> builder)
    {
        builder.ToTable("Promotions", table =>
        {
            table.HasCheckConstraint("CK_Promotions_DiscountValue", "\"DiscountValue\" >= 0");
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
                Id = SeedData.PromotionTops20,
                CampaignId = SeedData.CampaignSummer,
                Name = "Tops 20% Off",
                Description = "20% off all tops.",
                Type = PromotionType.PercentageDiscount,
                DiscountValue = 20m,
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
                PromotionId = SeedData.PromotionTops20,
                ProductId = SeedData.ProductTShirt
            },
            new PromotionProduct
            {
                PromotionId = SeedData.PromotionTops20,
                ProductId = SeedData.ProductHoodie
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
    }
}

public class CouponConfiguration : IEntityTypeConfiguration<Coupon>
{
    public void Configure(EntityTypeBuilder<Coupon> builder)
    {
        builder.ToTable("Coupons");

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
                PromotionId = SeedData.PromotionTops20,
                UsageLimit = 100,
                PerCustomerLimit = 1,
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

        builder.HasIndex(e => e.CouponId);
        builder.HasIndex(e => e.CustomerId);

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
