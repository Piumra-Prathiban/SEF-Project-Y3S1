using System.ComponentModel.DataAnnotations;

namespace SEF_Project.Api.DTOs.Shopping;

public class ProductSearchQuery
{
    /// <summary>Text matched against product names, descriptions, variant names, and SKUs.</summary>
    [StringLength(100)]
    public string? Search { get; set; }

    /// <summary>Limits results to an active category.</summary>
    public Guid? CategoryId { get; set; }

    /// <summary>Minimum active-variant price, inclusive.</summary>
    [Range(0, 999999999)]
    public decimal? MinPrice { get; set; }

    /// <summary>Maximum active-variant price, inclusive.</summary>
    [Range(0, 999999999)]
    public decimal? MaxPrice { get; set; }

    /// <summary>When true, returns only products with available inventory.</summary>
    public bool InStockOnly { get; set; }

    /// <summary>Allowed values: name, price, newest.</summary>
    [StringLength(20)]
    public string SortBy { get; set; } = "name";

    /// <summary>Allowed values: asc, desc.</summary>
    [StringLength(4)]
    public string SortDirection { get; set; } = "asc";

    /// <summary>One-based page number.</summary>
    [Range(1, 10000)]
    public int Page { get; set; } = 1;

    /// <summary>Number of products per page (maximum 100).</summary>
    [Range(1, 100)]
    public int PageSize { get; set; } = 12;
}

public class PagedProductResponse
{
    public IReadOnlyList<ShoppingProductResponse> Items { get; set; } =
        Array.Empty<ShoppingProductResponse>();

    public int Page { get; set; }

    public int PageSize { get; set; }

    public int TotalCount { get; set; }

    public int TotalPages { get; set; }

    public bool HasPreviousPage => Page > 1;

    public bool HasNextPage => Page < TotalPages;
}

public class ShoppingProductResponse
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public decimal MinimumPrice { get; set; }

    public bool IsAvailable { get; set; }

    public IReadOnlyList<ShoppingCategoryResponse> Categories { get; set; } =
        Array.Empty<ShoppingCategoryResponse>();

    public IReadOnlyList<ShoppingProductVariantResponse> Variants { get; set; } =
        Array.Empty<ShoppingProductVariantResponse>();
}

public class ShoppingProductVariantResponse
{
    public Guid Id { get; set; }

    public string Sku { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public decimal Price { get; set; }

    public int AvailableQuantity { get; set; }

    public bool IsAvailable { get; set; }

    public string? Size { get; set; }

    public string? Colour { get; set; }
}

public class ShoppingCategoryResponse
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;
}
