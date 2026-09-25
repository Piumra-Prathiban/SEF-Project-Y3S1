using System.ComponentModel.DataAnnotations;

namespace SEF_Project.Api.DTOs.Catalog;

/// <summary>Payload for creating a supplier.</summary>
public class SupplierCreateDto
{
    [Required]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [StringLength(200)]
    public string? ContactName { get; set; }

    [EmailAddress]
    [StringLength(320)]
    public string? Email { get; set; }

    [StringLength(50)]
    public string? Phone { get; set; }

    public bool IsActive { get; set; } = true;
}

/// <summary>Payload for updating an existing supplier.</summary>
public class SupplierUpdateDto
{
    [Required]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [StringLength(200)]
    public string? ContactName { get; set; }

    [EmailAddress]
    [StringLength(320)]
    public string? Email { get; set; }

    [StringLength(50)]
    public string? Phone { get; set; }

    public bool IsActive { get; set; } = true;
}

/// <summary>Supplier as returned by the API.</summary>
public class SupplierResponseDto
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? ContactName { get; set; }

    public string? Email { get; set; }

    public string? Phone { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}

/// <summary>Filters applied when listing suppliers.</summary>
public class SupplierQueryDto
{
    public string? Search { get; set; }

    public bool? IsActive { get; set; }
}
