using SEF_Project.Api.Models.Orders;

namespace SEF_Project.Api.Models.Marketing;

public class CouponRedemption : GuidEntity
{
    public Guid CouponId { get; set; }

    public Coupon Coupon { get; set; } = null!;

    public int CustomerId { get; set; }

    public Customer Customer { get; set; } = null!;

    public Guid? OrderId { get; set; }

    public Order? Order { get; set; }

    public DateTime RedeemedAt { get; set; }
}
