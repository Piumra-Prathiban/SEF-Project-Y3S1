using System.ComponentModel.DataAnnotations;

namespace SEF_Project.Api.DTOs.Recommendations;

/// <summary>Fashion-shopping preferences used to start a recommendation request.</summary>
public class RecommendationRequest : IValidatableObject
{
    private IReadOnlyList<string> _preferredColours = Array.Empty<string>();

    /// <summary>The event or situation the customer is dressing for.</summary>
    [Required]
    [StringLength(100, MinimumLength = 2)]
    public string Occasion { get; set; } = string.Empty;

    /// <summary>Maximum price for an individual recommended product.</summary>
    [Range(1, 999999999)]
    public decimal? Budget { get; set; }

    /// <summary>Preferred fashion colours. Catalogue support is capability-dependent.</summary>
    [MaxLength(10)]
    public IReadOnlyList<string> PreferredColours
    {
        get => _preferredColours;
        set => _preferredColours = value ?? Array.Empty<string>();
    }

    /// <summary>Preferred fashion size. Catalogue support is capability-dependent.</summary>
    [StringLength(50)]
    public string? PreferredSize { get; set; }

    /// <summary>Optional fashion style notes, such as formal, minimal, or relaxed.</summary>
    [StringLength(500)]
    public string? StylePreferences { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        for (var index = 0; index < PreferredColours.Count; index++)
        {
            var colour = PreferredColours[index];

            if (string.IsNullOrWhiteSpace(colour))
            {
                yield return new ValidationResult(
                    "Preferred colours cannot contain blank values.",
                    new[] { nameof(PreferredColours) });
            }
            else if (colour.Trim().Length > 50)
            {
                yield return new ValidationResult(
                    "Each preferred colour must be 50 characters or fewer.",
                    new[] { nameof(PreferredColours) });
            }
        }
    }
}

public class RecommendationResponse
{
    public Guid RequestId { get; set; }

    public string Status { get; set; } = "catalogue_candidates_ready";

    public DateTimeOffset CreatedAt { get; set; }

    public RecommendationCustomerResponse Customer { get; set; } = new();

    public RecommendationCriteriaResponse Criteria { get; set; } = new();

    public IReadOnlyList<RecommendationProductResponse> Products { get; set; } =
        Array.Empty<RecommendationProductResponse>();

    /// <summary>
    /// Preferences that could not be enforced by the current catalogue schema.
    /// </summary>
    public IReadOnlyList<string> UnappliedPreferences { get; set; } =
        Array.Empty<string>();

    /// <summary>
    /// Criteria relaxed only when an exact catalogue search returned no products.
    /// </summary>
    public IReadOnlyList<string> RelaxedCriteria { get; set; } =
        Array.Empty<string>();
}

public class RecommendationCustomerResponse
{
    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;
}

public class RecommendationCriteriaResponse
{
    public string Occasion { get; set; } = string.Empty;

    public decimal? Budget { get; set; }

    public IReadOnlyList<string> PreferredColours { get; set; } =
        Array.Empty<string>();

    public string? PreferredSize { get; set; }

    public string? StylePreferences { get; set; }
}

public class RecommendationProductResponse
{
    public Guid ProductId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public decimal MinimumAvailablePrice { get; set; }

    public IReadOnlyList<RecommendationCategoryResponse> Categories { get; set; } =
        Array.Empty<RecommendationCategoryResponse>();

    public IReadOnlyList<RecommendationVariantResponse> AvailableVariants { get; set; } =
        Array.Empty<RecommendationVariantResponse>();
}

public class RecommendationCategoryResponse
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;
}

public class RecommendationVariantResponse
{
    public Guid Id { get; set; }

    public string Sku { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public decimal Price { get; set; }

    public int AvailableQuantity { get; set; }
}
