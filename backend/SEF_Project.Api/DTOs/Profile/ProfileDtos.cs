using System.ComponentModel.DataAnnotations;

namespace SEF_Project.Api.DTOs.Profile;

public class ProfileResponse
{
    public int CustomerId { get; set; }

    public string Email { get; set; } = string.Empty;

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public DateTime MemberSince { get; set; }
}

public class UpdateProfileRequest
{
    [Required]
    [EmailAddress]
    [StringLength(320)]
    public string Email { get; set; } = string.Empty;

    [Required]
    [StringLength(50)]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    [StringLength(50)]
    public string LastName { get; set; } = string.Empty;
}

public abstract class AddressRequest
{
    [Required]
    [StringLength(50)]
    public string Label { get; set; } = string.Empty;

    [Required]
    [StringLength(200)]
    public string AddressLine1 { get; set; } = string.Empty;

    [StringLength(200)]
    public string? AddressLine2 { get; set; }

    [Required]
    [StringLength(100)]
    public string City { get; set; } = string.Empty;

    [StringLength(100)]
    public string? Province { get; set; }

    [Required]
    [StringLength(20, MinimumLength = 2)]
    [RegularExpression(
        "^[A-Za-z0-9][A-Za-z0-9 -]*$",
        ErrorMessage = "Postal code contains invalid characters.")]
    public string PostalCode { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string Country { get; set; } = "Sri Lanka";

    public bool IsDefault { get; set; }
}

public class CreateAddressRequest : AddressRequest
{
}

public class UpdateAddressRequest : AddressRequest
{
}

public class AddressResponse
{
    public int Id { get; set; }

    public string Label { get; set; } = string.Empty;

    public string AddressLine1 { get; set; } = string.Empty;

    public string? AddressLine2 { get; set; }

    public string City { get; set; } = string.Empty;

    public string? Province { get; set; }

    public string PostalCode { get; set; } = string.Empty;

    public string Country { get; set; } = string.Empty;

    public bool IsDefault { get; set; }
}
