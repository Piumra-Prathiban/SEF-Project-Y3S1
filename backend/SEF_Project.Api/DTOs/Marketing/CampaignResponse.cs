using SEF_Project.Api.Models.Enums;

namespace SEF_Project.Api.DTOs.Marketing;

public class CampaignResponse
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public DateTime StartDate { get; set; }

    public DateTime EndDate { get; set; }

    public CampaignStatus Status { get; set; }

    public int PromotionCount { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
