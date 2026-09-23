namespace SEF_Project.Api.Models.Shopping;

public class Wishlist : GuidEntity
{
    public int CustomerId { get; set; }

    public Customer Customer { get; set; } = null!;

    public ICollection<WishlistItem> Items { get; set; } =
        new List<WishlistItem>();
}
