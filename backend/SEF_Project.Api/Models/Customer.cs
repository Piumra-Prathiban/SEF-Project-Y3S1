using SEF_Project.Api.Models.Shopping;

namespace SEF_Project.Api.Models;

public class Customer : BaseEntity
{
    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public ICollection<Address> Addresses { get; set; } = new List<Address>();

    public Cart? Cart { get; set; }

    public Wishlist? Wishlist { get; set; }
}
