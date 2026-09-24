using System.ComponentModel.DataAnnotations;
using SEF_Project.Api.DTOs.Recommendations;

namespace SEF_Project.Api.Services.Recommendations;

public class RecommendationService : IRecommendationService
{
    private readonly IRecommendationOrchestrator _orchestrator;

    public RecommendationService(IRecommendationOrchestrator orchestrator)
    {
        _orchestrator = orchestrator;
    }

    public async Task<RecommendationResponse> StartAsync(
        int userId,
        RecommendationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateRequest(request);

        var context = Normalize(request);
        var result = await _orchestrator.CreateCandidatesAsync(
            userId,
            context,
            cancellationToken);

        return new RecommendationResponse
        {
            RequestId = Guid.NewGuid(),
            CreatedAt = DateTimeOffset.UtcNow,
            Customer = new RecommendationCustomerResponse
            {
                FirstName = result.Customer.FirstName,
                LastName = result.Customer.LastName
            },
            Criteria = new RecommendationCriteriaResponse
            {
                Occasion = context.Occasion,
                Budget = context.Budget,
                PreferredColours = context.PreferredColours,
                PreferredSize = context.PreferredSize,
                StylePreferences = context.StylePreferences
            },
            Products = result.Products
                .Select(MapProduct)
                .ToList(),
            UnappliedPreferences = result.UnappliedPreferences,
            RelaxedCriteria = result.RelaxedCriteria
        };
    }

    private static RecommendationContext Normalize(RecommendationRequest request)
    {
        var colours = request.PreferredColours
            .Select(colour => colour.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new RecommendationContext(
            request.Occasion.Trim(),
            request.Budget,
            colours,
            NormalizeOptional(request.PreferredSize),
            NormalizeOptional(request.StylePreferences));
    }

    private static RecommendationProductResponse MapProduct(
        RecommendationCatalogProduct product) => new()
        {
            ProductId = product.ProductId,
            Name = product.Name,
            Description = product.Description,
            MinimumAvailablePrice = product.MinimumAvailablePrice,
            Categories = product.Categories
                .Select(category => new RecommendationCategoryResponse
                {
                    Id = category.Id,
                    Name = category.Name
                })
                .ToList(),
            AvailableVariants = product.AvailableVariants
                .Select(variant => new RecommendationVariantResponse
                {
                    Id = variant.Id,
                    Sku = variant.Sku,
                    Name = variant.Name,
                    Price = variant.Price,
                    AvailableQuantity = variant.AvailableQuantity
                })
                .ToList()
        };

    private static void ValidateRequest(RecommendationRequest request)
    {
        var validationResults = new List<ValidationResult>();

        if (!Validator.TryValidateObject(
                request,
                new ValidationContext(request),
                validationResults,
                validateAllProperties: true))
        {
            throw new ArgumentException(
                validationResults[0].ErrorMessage ??
                "The recommendation request is invalid.");
        }
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
