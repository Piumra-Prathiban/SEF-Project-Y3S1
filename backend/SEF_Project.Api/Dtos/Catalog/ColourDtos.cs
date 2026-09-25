using System.ComponentModel.DataAnnotations;

namespace SEF_Project.Api.DTOs.Catalog;

public class ColourCreateDto
{
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [RegularExpression("^#[0-9A-Fa-f]{6}$")]
    public string? HexCode { get; set; }

    public bool IsActive { get; set; } = true;
}

public class ColourUpdateDto
{
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [RegularExpression("^#[0-9A-Fa-f]{6}$")]
    public string? HexCode { get; set; }

    public bool IsActive { get; set; } = true;
}

public class ColourResponseDto
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? HexCode { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
