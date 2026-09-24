namespace SEF_Project.Api.Services.Recommendations;

/// <summary>
/// Controlled customer lookup available to recommendation orchestration.
/// Implementations must enforce server-derived customer identity.
/// </summary>
public interface IRecommendationCustomerTool
{
    Task<RecommendationCustomerContext> GetCurrentCustomerAsync(
        int userId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Controlled, read-only catalogue search available to recommendation orchestration.
/// An AI implementation must use this boundary instead of database access.
/// </summary>
public interface IRecommendationCatalogTool
{
    RecommendationCatalogCapabilities Capabilities { get; }

    Task<IReadOnlyList<RecommendationCatalogProduct>> SearchAsync(
        RecommendationCatalogQuery query,
        CancellationToken cancellationToken = default);
}
