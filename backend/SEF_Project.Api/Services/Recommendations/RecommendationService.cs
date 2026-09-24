using System.ComponentModel.DataAnnotations;
using SEF_Project.Api.DTOs.Recommendations;

namespace SEF_Project.Api.Services.Recommendations;

public class RecommendationService : IRecommendationService
{
    private readonly IPersonalStylistAgent _agent;

    public RecommendationService(IPersonalStylistAgent agent)
    {
        _agent = agent;
    }

    public async Task<RecommendationResponse> StartAsync(
        int userId,
        RecommendationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateRequest(request);

        var context = Normalize(request);
        var result = await _agent.RunAsync(
            userId,
            context,
            cancellationToken);

        return new RecommendationResponse
        {
            WorkflowId = result.WorkflowId,
            Status = result.Execution.Status,
            CreatedAt = DateTimeOffset.UtcNow,
            Customer = result.Customer == null
                ? null
                : new RecommendationCustomerResponse
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
            Recommendations = result.Recommendations
                .Select(item => new ProductRecommendationResponse
                {
                    ProductId = item.ProductId,
                    VariantId = item.VariantId,
                    ProductName = item.ProductName,
                    VariantName = item.VariantName,
                    Sku = item.Sku,
                    Price = item.Price,
                    AvailableQuantity = item.AvailableQuantity,
                    Reason = item.Reason
                })
                .ToList(),
            UnappliedPreferences = result.UnappliedPreferences,
            RelaxedCriteria = result.RelaxedCriteria,
            Execution = new RecommendationExecutionSummaryResponse
            {
                AgentName = result.Execution.AgentName,
                Status = result.Execution.Status,
                ToolAttempts = result.Execution.ToolAttempts,
                SuccessfulToolExecutions =
                    result.Execution.SuccessfulToolExecutions,
                OutputValidated = result.Execution.OutputValidated,
                ErrorSummary = result.Execution.ErrorSummary
            }
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
