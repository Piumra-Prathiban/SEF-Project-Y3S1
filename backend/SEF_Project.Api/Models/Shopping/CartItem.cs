using SEF_Project.Api.Models.Catalog;

namespace SEF_Project.Api.Models.Shopping;

public class CartItem : GuidEntity
{
    public Guid CartId { get; set; }

    public Cart Cart { get; set; } = null!;

    public Guid ProductVariantId { get; set; }

    public ProductVariant ProductVariant { get; set; } = null!;

    public int Quantity { get; set; }
}
