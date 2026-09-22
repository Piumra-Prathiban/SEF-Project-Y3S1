using SEF_Project.Api.Models.Catalog;

namespace SEF_Project.Api.Models.Marketing;

public class PromotionProduct
{
    public Guid PromotionId { get; set; }

    public Promotion Promotion { get; set; } = null!;

    public Guid ProductId { get; set; }

    public Product Product { get; set; } = null!;
}
