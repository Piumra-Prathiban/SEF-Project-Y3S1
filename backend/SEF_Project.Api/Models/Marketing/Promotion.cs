using SEF_Project.Api.Models.Enums;

namespace SEF_Project.Api.Models.Marketing;

public class Promotion : GuidEntity
{
    public Guid? CampaignId { get; set; }

    public Campaign? Campaign { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public PromotionType Type { get; set; }

    public decimal DiscountValue { get; set; }

    public DateTime StartDate { get; set; }

    public DateTime EndDate { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<PromotionProduct> PromotionProducts { get; set; } =
        new List<PromotionProduct>();

    public ICollection<PromotionCategory> PromotionCategories { get; set; } =
        new List<PromotionCategory>();

    public ICollection<Coupon> Coupons { get; set; } = new List<Coupon>();
}
