namespace SEF_Project.Api.Models.Orders;

public class OrderAddress : GuidEntity
{
    public Guid OrderId { get; set; }

    public Order Order { get; set; } = null!;

    public string FullName { get; set; } = string.Empty;

    public string Line1 { get; set; } = string.Empty;

    public string? Line2 { get; set; }

    public string City { get; set; } = string.Empty;

    public string? Province { get; set; }

    public string PostalCode { get; set; } = string.Empty;

    public string Country { get; set; } = "Sri Lanka";

    public string? Phone { get; set; }
}
