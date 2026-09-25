using SEF_Project.Api.Models.Enums;

namespace SEF_Project.Api.DTOs.Marketing;

public class PromotionDiscountResponse
{
    public Guid PromotionId { get; set; }

    public string PromotionName { get; set; } = string.Empty;

    public PromotionType PromotionType { get; set; }

    public Guid ProductId { get; set; }

    public Guid ProductVariantId { get; set; }

    public string Sku { get; set; } = string.Empty;

    public decimal OriginalPrice { get; set; }

    public decimal DiscountAmount { get; set; }

    public decimal FinalPrice { get; set; }

    public string Currency { get; set; } = string.Empty;

    public DateTime CalculatedAt { get; set; }
}
