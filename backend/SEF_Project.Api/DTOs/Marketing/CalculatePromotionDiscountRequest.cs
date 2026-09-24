using SEF_Project.Api.DTOs.Common;

namespace SEF_Project.Api.DTOs.Marketing;

// Only identifiers are accepted: prices and discounts are always loaded from
// the database, never taken from the client.
public class CalculatePromotionDiscountRequest
{
    [NotEmptyGuid]
    public Guid PromotionId { get; set; }

    [NotEmptyGuid]
    public Guid ProductVariantId { get; set; }
}
