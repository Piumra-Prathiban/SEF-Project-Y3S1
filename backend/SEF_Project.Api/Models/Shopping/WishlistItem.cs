using SEF_Project.Api.Models.Catalog;

namespace SEF_Project.Api.Models.Shopping;

public class WishlistItem : GuidEntity
{
    public Guid WishlistId { get; set; }

    public Wishlist Wishlist { get; set; } = null!;

    public Guid ProductId { get; set; }

    public Product Product { get; set; } = null!;
}
