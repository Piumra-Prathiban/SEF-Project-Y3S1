using SEF_Project.Api.DTOs.Shopping;
using SEF_Project.Api.Services.Profile;
using SEF_Project.Api.Services.Shopping;

namespace SEF_Project.Api.Services.Recommendations;

public class RecommendationCustomerTool : IRecommendationCustomerTool
{
    private readonly IProfileService _profileService;

    public RecommendationCustomerTool(IProfileService profileService)
    {
        _profileService = profileService;
    }

    public async Task<RecommendationCustomerContext> GetCurrentCustomerAsync(
        int userId,
        CancellationToken cancellationToken = default)
    {
        var profile = await _profileService.GetProfileAsync(
            userId,
            cancellationToken);

        return new RecommendationCustomerContext(
            profile.CustomerId,
            profile.FirstName,
            profile.LastName);
    }
}

public class RecommendationCatalogTool : IRecommendationCatalogTool
{
    private readonly IProductSearchService _productSearchService;

    public RecommendationCatalogTool(IProductSearchService productSearchService)
    {
        _productSearchService = productSearchService;
    }

    public RecommendationCatalogCapabilities Capabilities { get; } =
        new(SupportsColour: false, SupportsSize: false);

    public async Task<IReadOnlyList<RecommendationCatalogProduct>> SearchAsync(
        RecommendationCatalogQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (query.Limit is < 1 or > 50)
        {
            throw new ArgumentException(
                "Recommendation catalogue limit must be between 1 and 50.");
        }

        var response = await _productSearchService.SearchAsync(
            new ProductSearchQuery
            {
                Search = NormalizeOptional(query.SearchText),
                MaxPrice = query.MaximumPrice,
                InStockOnly = true,
                SortBy = query.MaximumPrice.HasValue ? "price" : "newest",
                SortDirection = query.MaximumPrice.HasValue ? "asc" : "desc",
                Page = 1,
                PageSize = query.Limit
            },
            cancellationToken);

        return response.Items
            .Select(product => MapProduct(product, query.MaximumPrice))
            .Where(product => product.AvailableVariants.Count > 0)
            .ToList();
    }

    private static RecommendationCatalogProduct MapProduct(
        ShoppingProductResponse product,
        decimal? maximumPrice)
    {
        var variants = product.Variants
            .Where(variant =>
                variant.IsAvailable &&
                (!maximumPrice.HasValue || variant.Price <= maximumPrice.Value))
            .Select(variant => new RecommendationCatalogVariant(
                variant.Id,
                variant.Sku,
                variant.Name,
                variant.Price,
                variant.AvailableQuantity))
            .ToList();

        return new RecommendationCatalogProduct(
            product.Id,
            product.Name,
            product.Description,
            variants.Count == 0
                ? 0
                : variants.Min(variant => variant.Price),
            product.Categories
                .Select(category => new RecommendationCatalogCategory(
                    category.Id,
                    category.Name))
                .ToList(),
            variants);
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
