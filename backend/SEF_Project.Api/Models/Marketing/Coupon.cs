namespace SEF_Project.Api.Models.Marketing;

public class Coupon : GuidEntity
{
    public string Code { get; set; } = string.Empty;

    public Guid? PromotionId { get; set; }

    public Promotion? Promotion { get; set; }

    public int? UsageLimit { get; set; }

    public int? PerCustomerLimit { get; set; }

    public DateTime StartsAt { get; set; }

    public DateTime EndsAt { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<CouponRedemption> Redemptions { get; set; } =
        new List<CouponRedemption>();
}
