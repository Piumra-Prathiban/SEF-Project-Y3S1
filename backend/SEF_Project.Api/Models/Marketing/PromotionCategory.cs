using SEF_Project.Api.Models.Catalog;

namespace SEF_Project.Api.Models.Marketing;

public class PromotionCategory
{
    public Guid PromotionId { get; set; }

    public Promotion Promotion { get; set; } = null!;

    public Guid CategoryId { get; set; }

    public Category Category { get; set; } = null!;
}
