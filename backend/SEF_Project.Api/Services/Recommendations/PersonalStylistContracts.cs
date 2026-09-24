namespace SEF_Project.Api.Services.Recommendations;

public static class PersonalStylistAgentContract
{
    public const string AgentName = "Personal Stylist Agent";
    public const int MaximumRecommendations = 5;
    public const int MaximumCandidateVariants = 50;
}

public static class PersonalStylistToolNames
{
    public const string ProductSearch = "product_search";
    public const string CustomerPreference = "customer_preference";
    public const string Wishlist = "wishlist";
    public const string ProductAvailability = "product_availability";

    public static IReadOnlySet<string> Allowed { get; } =
        new HashSet<string>(StringComparer.Ordinal)
        {
            ProductSearch,
            CustomerPreference,
            Wishlist,
            ProductAvailability
        };
}

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

public sealed record CustomerPreferenceToolInput(
    int UserId,
    RecommendationContext RequestedPreferences);

public sealed record CustomerPreferenceToolOutput(
    RecommendationCustomerContext Customer,
    RecommendationContext Preferences);

public sealed record WishlistToolInput(int UserId);

public sealed record WishlistToolItem(
    Guid ProductId,
    string ProductName,
    bool IsAvailable);

public sealed record WishlistToolOutput(
    IReadOnlyList<WishlistToolItem> Items);

public sealed record ProductSearchToolInput(
    string? SearchText,
    decimal? MaximumPrice,
    int Limit = 12);

public sealed record ProductSearchToolOutput(
    IReadOnlyList<RecommendationCatalogProduct> Products,
    RecommendationCatalogCapabilities Capabilities);

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
    int AvailableQuantity,
    string? Size = null,
    string? Colour = null);

public sealed record ProductAvailabilityToolItem(
    Guid ProductId,
    Guid VariantId);

public sealed record ProductAvailabilityToolInput(
    IReadOnlyList<ProductAvailabilityToolItem> Items);

public sealed record VerifiedProductAvailability(
    Guid ProductId,
    Guid VariantId,
    string ProductName,
    string VariantName,
    string Sku,
    decimal Price,
    int AvailableQuantity,
    string? Size = null,
    string? Colour = null);

public sealed record ProductAvailabilityToolOutput(
    IReadOnlyList<VerifiedProductAvailability> AvailableVariants);

public sealed record PersonalStylistModelInput(
    RecommendationCustomerContext Customer,
    RecommendationContext Preferences,
    IReadOnlyList<WishlistToolItem> WishlistItems,
    IReadOnlyList<RecommendationCatalogProduct> CatalogueProducts,
    IReadOnlyList<VerifiedProductAvailability> AvailableVariants,
    IReadOnlyList<string> RelaxedCriteria);

public sealed record PersonalStylistDraftRecommendation(
    Guid ProductId,
    Guid VariantId,
    decimal Price,
    int Quantity,
    string? Size,
    string? Colour,
    string Reason);

public sealed record PersonalStylistModelOutput(
    IReadOnlyList<PersonalStylistDraftRecommendation> Recommendations);

public sealed record PersonalStylistRecommendation(
    Guid ProductId,
    Guid VariantId,
    string ProductName,
    string VariantName,
    string Sku,
    decimal Price,
    int Quantity,
    int AvailableQuantity,
    string? Size,
    string? Colour,
    string Reason);

public sealed record RecommendationValidationCheck(
    string Rule,
    bool IsValid,
    string Message);

public sealed record PersonalStylistExecutionSummary(
    string AgentName,
    string Status,
    int ToolAttempts,
    int SuccessfulToolExecutions,
    bool OutputValidated,
    string? ErrorSummary,
    IReadOnlyList<RecommendationValidationCheck> ValidationResults);

public sealed record PersonalStylistAgentResult(
    Guid WorkflowId,
    RecommendationCustomerContext? Customer,
    RecommendationContext Preferences,
    IReadOnlyList<PersonalStylistRecommendation> Recommendations,
    IReadOnlyList<string> UnappliedPreferences,
    IReadOnlyList<string> RelaxedCriteria,
    PersonalStylistExecutionSummary Execution);

public sealed record AgentWorkflowHandle(Guid WorkflowId, Guid StepId);

public sealed record AgentToolExecutionHandle(Guid ToolExecutionId);

public sealed record AgentOutputValidation(
    bool IsValid,
    string Message,
    IReadOnlyList<PersonalStylistRecommendation> Recommendations,
    IReadOnlyList<RecommendationValidationCheck> Checks);

public class PersonalStylistToolException : Exception
{
    public PersonalStylistToolException(
        string toolName,
        string message,
        bool isTransient = false,
        Exception? innerException = null)
        : base(message, innerException)
    {
        ToolName = toolName;
        IsTransient = isTransient;
    }

    public string ToolName { get; }

    public bool IsTransient { get; }
}
