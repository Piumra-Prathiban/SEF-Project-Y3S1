namespace SEF_Project.Api.Models;

public class Address
{
    public int Id { get; set; }

    public int CustomerId { get; set; }

    public Customer Customer { get; set; } = null!;

    public string Label { get; set; } = string.Empty;

    public string AddressLine1 { get; set; } = string.Empty;

    public string? AddressLine2 { get; set; }

    public string City { get; set; } = string.Empty;

    public string? Province { get; set; }

    public string PostalCode { get; set; } = string.Empty;

    public string Country { get; set; } = "Sri Lanka";

    public bool IsDefault { get; set; }
}