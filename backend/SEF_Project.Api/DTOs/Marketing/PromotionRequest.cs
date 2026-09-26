using System.ComponentModel.DataAnnotations;
using SEF_Project.Api.Models.Enums;

namespace SEF_Project.Api.DTOs.Marketing;

// Used for both POST (create) and PUT (full replace).
public class PromotionRequest : IValidatableObject
{
    // Bounds the IN (...) lists built from the targets.
    public const int MaxTargets = 500;

    [Required]
    [StringLength(200, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    [StringLength(2000)]
    public string? Description { get; set; }

    [Required]
    [EnumDataType(typeof(PromotionType))]
    public PromotionType? Type { get; set; }

    [Range(typeof(decimal), "0", "999999999")]
    public decimal DiscountValue { get; set; }

    [Required]
    public DateTime? StartDate { get; set; }

    [Required]
    public DateTime? EndDate { get; set; }

    public bool IsActive { get; set; } = true;

    public Guid? CampaignId { get; set; }

    [MaxLength(MaxTargets)]
    public List<Guid> ProductIds { get; set; } = new();

    [MaxLength(MaxTargets)]
    public List<Guid> CategoryIds { get; set; } = new();

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (StartDate is not null && EndDate is not null && EndDate < StartDate)
        {
            yield return new ValidationResult(
                "End date must be on or after the start date.",
                new[] { nameof(EndDate) });
        }

        if (Type == PromotionType.PercentageDiscount
            && (DiscountValue <= 0 || DiscountValue > 100))
        {
            yield return new ValidationResult(
                "Percentage discount must be greater than 0 and at most 100.",
                new[] { nameof(DiscountValue) });
        }

        if (Type == PromotionType.FixedAmountDiscount && DiscountValue <= 0)
        {
            yield return new ValidationResult(
                "Fixed discount must be greater than zero.",
                new[] { nameof(DiscountValue) });
        }

        if (Type is PromotionType.PercentageDiscount or PromotionType.FixedAmountDiscount
            && ProductIds.Count == 0
            && CategoryIds.Count == 0)
        {
            yield return new ValidationResult(
                "A price discount must target at least one product or category.",
                new[] { nameof(ProductIds) });
        }

        if (CampaignId == Guid.Empty
            || ProductIds.Contains(Guid.Empty)
            || CategoryIds.Contains(Guid.Empty))
        {
            yield return new ValidationResult(
                "Identifiers must not be empty.",
                new[] { nameof(CampaignId) });
        }
    }
}
