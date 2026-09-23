using SEF_Project.Api.DTOs.AgenticAI;

namespace SEF_Project.Api.Services.AgenticAI;

public static class InventoryAgentOutputValidator
{
    private static readonly HashSet<string> AllowedActions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "RESTOCK",
            "MONITOR",
            "NO_ACTION"
        };

    public static IReadOnlyList<string> Validate(
        InventoryAgentOutputDto? output)
    {
        var errors = new List<string>();

        if (output is null)
        {
            errors.Add("Agent output is missing.");
            return errors;
        }

        if (output.Recommendations is null)
        {
            errors.Add("Recommendations must be present.");
            return errors;
        }

        foreach (var recommendation in output.Recommendations)
        {
            if (recommendation.VariantId == Guid.Empty)
            {
                errors.Add("Recommendation variantId must be a non-empty GUID.");
            }

            if (recommendation.CurrentStock < 0)
            {
                errors.Add("Recommendation currentStock cannot be negative.");
            }

            if (recommendation.ReorderLevel < 0)
            {
                errors.Add("Recommendation reorderLevel cannot be negative.");
            }

            if (!AllowedActions.Contains(recommendation.RecommendedAction))
            {
                errors.Add(
                    "Recommendation recommendedAction must be RESTOCK, MONITOR or NO_ACTION.");
            }

            if (string.IsNullOrWhiteSpace(recommendation.Reason))
            {
                errors.Add("Recommendation reason is required.");
            }
        }

        return errors;
    }
}
