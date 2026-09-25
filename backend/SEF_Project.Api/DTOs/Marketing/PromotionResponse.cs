using SEF_Project.Api.Models.Enums;

namespace SEF_Project.Api.DTOs.Marketing;

public class PromotionResponse
{
    public Guid Id { get; set; }

    public Guid? CampaignId { get; set; }

    public string? CampaignName { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public PromotionType Type { get; set; }

    public decimal DiscountValue { get; set; }

    public DateTime StartDate { get; set; }

    public DateTime EndDate { get; set; }

    public bool IsActive { get; set; }

    public List<Guid> ProductIds { get; set; } = new();

    public List<Guid> CategoryIds { get; set; } = new();

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
