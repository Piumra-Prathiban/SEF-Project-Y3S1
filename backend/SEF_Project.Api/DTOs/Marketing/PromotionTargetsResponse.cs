namespace SEF_Project.Api.DTOs.Marketing;

// Lookup lists for choosing what a promotion targets (read-only).
public class PromotionTargetsResponse
{
    public List<PromotionTargetOption> Products { get; set; } = new();

    public List<PromotionTargetOption> Categories { get; set; } = new();
}

public class PromotionTargetOption
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public bool IsActive { get; set; }
}
