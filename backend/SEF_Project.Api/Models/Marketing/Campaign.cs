namespace SEF_Project.Api.Models.Marketing;

public class Campaign : GuidEntity
{
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public DateTime StartDate { get; set; }

    public DateTime EndDate { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<Promotion> Promotions { get; set; } = new List<Promotion>();
}
