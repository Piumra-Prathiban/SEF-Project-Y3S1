using System.ComponentModel.DataAnnotations;
using SEF_Project.Api.Models.Enums;

namespace SEF_Project.Api.DTOs.Marketing;

// Used for both POST (create) and PUT (full replace).
public class CampaignRequest : IValidatableObject
{
    [Required]
    [StringLength(200, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    [StringLength(2000)]
    public string? Description { get; set; }

    [Required]
    public DateTime? StartDate { get; set; }

    [Required]
    public DateTime? EndDate { get; set; }

    [EnumDataType(typeof(CampaignStatus))]
    public CampaignStatus Status { get; set; } = CampaignStatus.Draft;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (StartDate is not null && EndDate is not null && EndDate < StartDate)
        {
            yield return new ValidationResult(
                "End date must be on or after the start date.",
                new[] { nameof(EndDate) });
        }
    }
}
