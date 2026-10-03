namespace SEF_Project.Api.Services.Recommendations;

/// <summary>
/// Safe baseline reasoning model. It ranks grounded catalogue candidates and
/// emits identifiers plus short fashion-shopping reasons. It has no tools or
/// database access and can later be replaced by a model-backed implementation.
/// </summary>
public class GroundedPersonalStylistModel : IPersonalStylistRecommendationModel
{
    public Task<PersonalStylistModelOutput> GenerateAsync(
        PersonalStylistModelInput input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        cancellationToken.ThrowIfCancellationRequested();

        var wishlistProductIds = input.WishlistItems
            .Where(item => item.IsAvailable)
            .Select(item => item.ProductId)
            .ToHashSet();
        var availability = input.AvailableVariants
            .GroupBy(item => item.ProductId)
            .ToDictionary(
                group => group.Key,
                group => group.OrderBy(item => item.Price).ToList());
        var occasionRelaxed = input.RelaxedCriteria.Contains(
            "occasion",
            StringComparer.Ordinal);

        var rankedProducts = input.CatalogueProducts
            .Where(product => availability.ContainsKey(product.ProductId))
            .OrderByDescending(product =>
                wishlistProductIds.Contains(product.ProductId))
            .ThenBy(product => product.MinimumAvailablePrice)
            .ThenBy(product => product.Name)
            .ToList();
        var recommendations = new List<PersonalStylistDraftRecommendation>();
        decimal total = 0;

        foreach (var product in rankedProducts)
        {
            var variant = SelectMatchingVariant(
                availability[product.ProductId],
                input.Preferences);

            if (variant is null)
            {
                continue;
            }

            if (input.Preferences.Budget.HasValue &&
                total + variant.Price > input.Preferences.Budget.Value)
            {
                continue;
            }

            recommendations.Add(new PersonalStylistDraftRecommendation(
                product.ProductId,
                variant.VariantId,
                variant.Price,
                Quantity: 1,
                variant.Size,
                variant.Colour,
                BuildReason(
                    input.Preferences,
                    wishlistProductIds.Contains(product.ProductId),
                    occasionRelaxed)));
            total += variant.Price;

            if (recommendations.Count ==
                PersonalStylistAgentContract.MaximumRecommendations)
            {
                break;
            }
        }

        return Task.FromResult(new PersonalStylistModelOutput(recommendations));
    }

    private static VerifiedProductAvailability? SelectMatchingVariant(
        IReadOnlyList<VerifiedProductAvailability> variants,
        RecommendationContext preferences)
    {
        var candidates = variants;

        if (!string.IsNullOrWhiteSpace(preferences.PreferredSize))
        {
            candidates = candidates
                .Where(variant => string.Equals(
                    variant.Size,
                    preferences.PreferredSize,
                    StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        if (preferences.PreferredColours.Count > 0)
        {
            candidates = candidates
                .Where(variant => preferences.PreferredColours.Contains(
                    variant.Colour ?? string.Empty,
                    StringComparer.OrdinalIgnoreCase))
                .ToList();
        }

        // The list is already ordered by price, so this is the cheapest
        // variant that satisfies the requested size and colour.
        return candidates.FirstOrDefault();
    }

    private static string BuildReason(
        RecommendationContext preferences,
        bool isWishlisted,
        bool occasionRelaxed)
    {
        var source = isWishlisted
            ? "Saved in your wishlist and currently available"
            : "Currently available in the catalogue";
        var occasion = occasionRelaxed
            ? string.Empty
            : $" for {preferences.Occasion}";
        var budget = preferences.Budget.HasValue
            ? " within your budget"
            : string.Empty;

        return $"{source}{occasion}{budget}.";
    }
}
