namespace SEF_Project.Api.DTOs.Storefront;

/// <summary>
/// Filtering/sorting options for the public storefront listing.
/// </summary>
public class StorefrontProductQueryDto
{
    public string? Search { get; set; }

    public Guid? CategoryId { get; set; }

    public string? Size { get; set; }

    public string? Colour { get; set; }

    public decimal? MinPrice { get; set; }

    public decimal? MaxPrice { get; set; }

    /// <summary>"name" (default) or "price".</summary>
    public string? SortBy { get; set; }

    /// <summary>"asc" (default) or "desc".</summary>
    public string? SortDirection { get; set; }

    public int Limit { get; set; } = 24;
}
