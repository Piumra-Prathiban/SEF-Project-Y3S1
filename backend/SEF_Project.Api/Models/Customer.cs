namespace SEF_Project.Api.Models;

public class Customer
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public User User { get; set; } = null!;

    public ICollection<Address> Addresses { get; set; } = new List<Address>();
}