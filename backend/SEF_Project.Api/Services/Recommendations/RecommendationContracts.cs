namespace SEF_Project.Api.Services.Recommendations;

public sealed record RecommendationContext(
    string Occasion,
    decimal? Budget,
    IReadOnlyList<string> PreferredColours,
    string? PreferredSize,
    string? StylePreferences);

public sealed record RecommendationCustomerContext(
    int CustomerId,
    string FirstName,
    string LastName);

public sealed record RecommendationCatalogQuery(
    string? SearchText,
    decimal? MaximumPrice,
    int Limit = 12);

public sealed record RecommendationCatalogCapabilities(
    bool SupportsColour,
    bool SupportsSize);

public sealed record RecommendationCatalogProduct(
    Guid ProductId,
    string Name,
    string? Description,
    decimal MinimumAvailablePrice,
    IReadOnlyList<RecommendationCatalogCategory> Categories,
    IReadOnlyList<RecommendationCatalogVariant> AvailableVariants);

public sealed record RecommendationCatalogCategory(Guid Id, string Name);

public sealed record RecommendationCatalogVariant(
    Guid Id,
    string Sku,
    string Name,
    decimal Price,
    int AvailableQuantity);

public sealed record RecommendationOrchestrationResult(
    RecommendationCustomerContext Customer,
    IReadOnlyList<RecommendationCatalogProduct> Products,
    IReadOnlyList<string> UnappliedPreferences,
    IReadOnlyList<string> RelaxedCriteria);
