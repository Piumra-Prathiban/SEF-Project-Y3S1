namespace SEF_Project.Api.Services.Recommendations;

/// <summary>
/// Deterministic catalogue-backed foundation. A future agentic implementation
/// can replace this orchestrator while retaining the same controlled tools.
/// </summary>
public class CatalogRecommendationOrchestrator : IRecommendationOrchestrator
{
    private readonly IRecommendationCustomerTool _customerTool;
    private readonly IRecommendationCatalogTool _catalogTool;

    public CatalogRecommendationOrchestrator(
        IRecommendationCustomerTool customerTool,
        IRecommendationCatalogTool catalogTool)
    {
        _customerTool = customerTool;
        _catalogTool = catalogTool;
    }

    public async Task<RecommendationOrchestrationResult> CreateCandidatesAsync(
        int userId,
        RecommendationContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var customer = await _customerTool.GetCurrentCustomerAsync(
            userId,
            cancellationToken);
        var products = await _catalogTool.SearchAsync(
            new RecommendationCatalogQuery(
                context.Occasion,
                context.Budget),
            cancellationToken);
        var relaxedCriteria = new List<string>();

        if (products.Count == 0)
        {
            products = await _catalogTool.SearchAsync(
                new RecommendationCatalogQuery(
                    SearchText: null,
                    MaximumPrice: context.Budget),
                cancellationToken);
            relaxedCriteria.Add("occasion");
        }

        var unappliedPreferences = new List<string>();

        if (context.PreferredColours.Count > 0 &&
            !_catalogTool.Capabilities.SupportsColour)
        {
            unappliedPreferences.Add("preferredColours");
        }

        if (context.PreferredSize != null &&
            !_catalogTool.Capabilities.SupportsSize)
        {
            unappliedPreferences.Add("preferredSize");
        }

        if (context.StylePreferences != null)
        {
            unappliedPreferences.Add("stylePreferences");
        }

        return new RecommendationOrchestrationResult(
            customer,
            products,
            unappliedPreferences,
            relaxedCriteria);
    }
}
