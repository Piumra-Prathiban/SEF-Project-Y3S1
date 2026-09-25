using SEF_Project.Api.DTOs.Shopping;
using SEF_Project.Api.Services.Profile;
using SEF_Project.Api.Services.Shopping;

namespace SEF_Project.Api.Services.Recommendations;

public class CustomerPreferenceTool : ICustomerPreferenceTool
{
    private readonly IProfileService _profileService;

    public CustomerPreferenceTool(IProfileService profileService)
    {
        _profileService = profileService;
    }

    public async Task<CustomerPreferenceToolOutput> ExecuteAsync(
        CustomerPreferenceToolInput input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        PersonalStylistToolValidation.ValidateUserId(input.UserId);
        PersonalStylistToolValidation.ValidatePreferences(
            input.RequestedPreferences);

        var profile = await _profileService.GetProfileAsync(
            input.UserId,
            cancellationToken);

        return new CustomerPreferenceToolOutput(
            new RecommendationCustomerContext(
                profile.CustomerId,
                profile.FirstName,
                profile.LastName),
            input.RequestedPreferences);
    }
}

public class WishlistTool : IWishlistTool
{
    private readonly IWishlistService _wishlistService;

    public WishlistTool(IWishlistService wishlistService)
    {
        _wishlistService = wishlistService;
    }

    public async Task<WishlistToolOutput> ExecuteAsync(
        WishlistToolInput input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        PersonalStylistToolValidation.ValidateUserId(input.UserId);

        var wishlist = await _wishlistService.GetWishlistAsync(
            input.UserId,
            cancellationToken);

        return new WishlistToolOutput(
            wishlist.Items
                .Select(item => new WishlistToolItem(
                    item.ProductId,
                    item.ProductName,
                    item.IsAvailable))
                .ToList());
    }
}

public class ProductSearchTool : IProductSearchTool
{
    private readonly IProductSearchService _productSearchService;

    public ProductSearchTool(IProductSearchService productSearchService)
    {
        _productSearchService = productSearchService;
    }

    public async Task<ProductSearchToolOutput> ExecuteAsync(
        ProductSearchToolInput input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        PersonalStylistToolValidation.ValidateProductSearch(input);

        var response = await _productSearchService.SearchAsync(
            new ProductSearchQuery
            {
                Search = NormalizeOptional(input.SearchText),
                MaxPrice = input.MaximumPrice,
                InStockOnly = true,
                SortBy = input.MaximumPrice.HasValue ? "price" : "newest",
                SortDirection = input.MaximumPrice.HasValue ? "asc" : "desc",
                Page = 1,
                PageSize = input.Limit
            },
            cancellationToken);

        var products = response.Items
            .Select(product => MapProduct(product, input.MaximumPrice))
            .Where(product => product.AvailableVariants.Count > 0)
            .ToList();

        return new ProductSearchToolOutput(
            products,
            new RecommendationCatalogCapabilities(
                SupportsColour: false,
                SupportsSize: false));
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
                variant.AvailableQuantity,
                Size: null,
                Colour: null))
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

public class ProductAvailabilityTool : IProductAvailabilityTool
{
    private readonly IProductAvailabilityService _availabilityService;

    public ProductAvailabilityTool(
        IProductAvailabilityService availabilityService)
    {
        _availabilityService = availabilityService;
    }

    public async Task<ProductAvailabilityToolOutput> ExecuteAsync(
        ProductAvailabilityToolInput input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        PersonalStylistToolValidation.ValidateAvailability(input);

        var expectedProducts = input.Items
            .GroupBy(item => item.VariantId)
            .ToDictionary(group => group.Key, group => group.First().ProductId);
        var available = await _availabilityService.GetAvailableVariantsAsync(
            expectedProducts.Keys.ToList(),
            cancellationToken);

        return new ProductAvailabilityToolOutput(
            available
                .Where(variant =>
                    expectedProducts.TryGetValue(
                        variant.VariantId,
                        out var productId) &&
                    productId == variant.ProductId)
                .Select(variant => new VerifiedProductAvailability(
                    variant.ProductId,
                    variant.VariantId,
                    variant.ProductName,
                    variant.VariantName,
                    variant.Sku,
                    variant.Price,
                    variant.AvailableQuantity,
                    variant.Size,
                    variant.Colour))
                .ToList());
    }
}

internal static class PersonalStylistToolValidation
{
    public static void ValidateUserId(int userId)
    {
        if (userId <= 0)
        {
            throw new ArgumentException("A valid authenticated user is required.");
        }
    }

    public static void ValidatePreferences(RecommendationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (string.IsNullOrWhiteSpace(context.Occasion) ||
            context.Occasion.Length is < 2 or > 100)
        {
            throw new ArgumentException(
                "Occasion must contain between 2 and 100 characters.");
        }

        if (context.Budget is <= 0 or > 999999999)
        {
            throw new ArgumentException("Budget must be a positive valid price.");
        }

        if (context.PreferredColours.Count > 10 ||
            context.PreferredColours.Any(colour =>
                string.IsNullOrWhiteSpace(colour) || colour.Length > 50))
        {
            throw new ArgumentException("Preferred colours are invalid.");
        }

        if (context.PreferredSize?.Length > 50 ||
            context.StylePreferences?.Length > 500)
        {
            throw new ArgumentException("Fashion preferences are too long.");
        }
    }

    public static void ValidateProductSearch(ProductSearchToolInput input)
    {
        if (input.SearchText?.Length > 100)
        {
            throw new ArgumentException("Product search text is too long.");
        }

        if (input.MaximumPrice is <= 0 or > 999999999)
        {
            throw new ArgumentException(
                "Maximum price must be a positive valid price.");
        }

        if (input.Limit is < 1 or > 50)
        {
            throw new ArgumentException(
                "Product search limit must be between 1 and 50.");
        }
    }

    public static void ValidateAvailability(ProductAvailabilityToolInput input)
    {
        if (input.Items.Count is < 1 or >
            PersonalStylistAgentContract.MaximumCandidateVariants)
        {
            throw new ArgumentException(
                "Product availability requires between 1 and 50 candidates.");
        }

        if (input.Items.Any(item =>
            item.ProductId == Guid.Empty || item.VariantId == Guid.Empty))
        {
            throw new ArgumentException(
                "Product and variant identifiers cannot be empty.");
        }

        if (input.Items.Select(item => item.VariantId).Distinct().Count() !=
            input.Items.Count)
        {
            throw new ArgumentException(
                "Product availability candidates cannot contain duplicates.");
        }
    }
}
