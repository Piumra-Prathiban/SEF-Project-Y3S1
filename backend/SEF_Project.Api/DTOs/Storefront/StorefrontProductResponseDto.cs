namespace SEF_Project.Api.DTOs.Storefront;

/// <summary>
/// Public storefront shape: only what a shopper needs to see. Deliberately
/// slimmer than the staff catalog DTOs (no supplier, stock counts, SKUs or
/// inactive rows) because these endpoints are anonymous.
/// </summary>
public class StorefrontProductResponseDto
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string? ImageUrl { get; set; }

    public Guid CategoryId { get; set; }

    public string CategoryName { get; set; } = string.Empty;

    public string CollectionName { get; set; } = string.Empty;

    public decimal PriceFrom { get; set; }

    public decimal PriceTo { get; set; }

    public List<string> Sizes { get; set; } = new();

    public List<StorefrontColourResponseDto> Colours { get; set; } = new();

    /// <summary>
    /// Buyable variants (active only) so the storefront can offer a size/colour
    /// picker and add the chosen variant to a cart.
    /// </summary>
    public List<StorefrontVariantResponseDto> Variants { get; set; } = new();

    public bool InStock { get; set; }
}

public class StorefrontColourResponseDto
{
    public string Name { get; set; } = string.Empty;

    public string? HexCode { get; set; }
}

public class StorefrontVariantResponseDto
{
    public Guid Id { get; set; }

    public string SizeName { get; set; } = string.Empty;

    public string ColourName { get; set; } = string.Empty;

    public string? ColourHex { get; set; }

    public decimal Price { get; set; }

    public bool InStock { get; set; }
}
