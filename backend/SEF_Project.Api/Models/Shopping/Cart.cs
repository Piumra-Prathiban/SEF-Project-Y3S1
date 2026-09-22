namespace SEF_Project.Api.Models.Shopping;

public class Cart : GuidEntity
{
    public int CustomerId { get; set; }

    public Customer Customer { get; set; } = null!;

    public ICollection<CartItem> Items { get; set; } = new List<CartItem>();
}
