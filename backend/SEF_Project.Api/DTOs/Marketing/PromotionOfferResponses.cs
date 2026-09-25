using SEF_Project.Api.Models.Enums;

namespace SEF_Project.Api.DTOs.Marketing;

// Customer-facing promotion offers. All prices are calculated by the server
// (PromotionDiscountCalculator); clients only display them.

public class PromotionProductsResponse
{
    public Guid PromotionId { get; set; }

    public string PromotionName { get; set; } = string.Empty;

    /// <summary>False for promotion types that do not discount item prices (e.g. free shipping).</summary>
    public bool HasPriceDiscount { get; set; }

    public List<ProductOfferResponse> Products { get; set; } = new();
}

public class ProductOfferResponse
{
    public Guid ProductId { get; set; }

    public string ProductName { get; set; } = string.Empty;

    public string? Description { get; set; }

    public List<VariantOfferResponse> Variants { get; set; } = new();
}

public class VariantOfferResponse
{
    public Guid ProductVariantId { get; set; }

    public string Sku { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public decimal OriginalPrice { get; set; }

    public decimal DiscountAmount { get; set; }

    public decimal FinalPrice { get; set; }

    /// <summary>The promotion that produced FinalPrice; null when no discount applies.</summary>
    public Guid? PromotionId { get; set; }

    public string? PromotionName { get; set; }

    public string Currency { get; set; } = string.Empty;
}

public class ProductPromotionsResponse
{
    public Guid ProductId { get; set; }

    public string ProductName { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool HasActivePromotion { get; set; }

    public List<PromotionSummaryResponse> Promotions { get; set; } = new();

    /// <summary>Each variant priced with its best live promotion (largest discount).</summary>
    public List<VariantOfferResponse> Variants { get; set; } = new();
}

public class PromotionSummaryResponse
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public PromotionType Type { get; set; }

    public decimal DiscountValue { get; set; }

    public DateTime StartDate { get; set; }

    public DateTime EndDate { get; set; }
}
