namespace SEF_Project.Api.Services.Recommendations;

public class PersonalStylistOutputValidator : IPersonalStylistOutputValidator
{
    public AgentOutputValidation Validate(
        PersonalStylistModelOutput? output,
        PersonalStylistModelInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (output == null)
        {
            return Invalid("The recommendation model returned no structured output.");
        }

        if (output.Recommendations.Count >
            PersonalStylistAgentContract.MaximumRecommendations)
        {
            return Invalid("The recommendation model exceeded the result limit.");
        }

        var duplicateVariant = output.Recommendations
            .GroupBy(item => item.VariantId)
            .Any(group => group.Count() > 1);

        if (duplicateVariant)
        {
            return Invalid("The recommendation model returned duplicate variants.");
        }

        var cataloguePairs = input.CatalogueProducts
            .SelectMany(product => product.AvailableVariants.Select(variant =>
                (product.ProductId, VariantId: variant.Id)))
            .ToHashSet();
        var availability = input.AvailableVariants.ToDictionary(
            item => (item.ProductId, item.VariantId));
        var recommendations = new List<PersonalStylistRecommendation>();

        foreach (var draft in output.Recommendations)
        {
            if (draft.ProductId == Guid.Empty || draft.VariantId == Guid.Empty ||
                string.IsNullOrWhiteSpace(draft.Reason) ||
                draft.Reason.Trim().Length > 300)
            {
                return Invalid("The recommendation model returned malformed fields.");
            }

            var key = (draft.ProductId, draft.VariantId);

            if (!cataloguePairs.Contains(key) ||
                !availability.TryGetValue(key, out var verified) ||
                verified.AvailableQuantity <= 0)
            {
                return Invalid(
                    "The recommendation model referenced an unverified catalogue variant.");
            }

            recommendations.Add(new PersonalStylistRecommendation(
                verified.ProductId,
                verified.VariantId,
                verified.ProductName,
                verified.VariantName,
                verified.Sku,
                verified.Price,
                verified.AvailableQuantity,
                draft.Reason.Trim()));
        }

        return new AgentOutputValidation(
            true,
            "All recommendations reference available catalogue variants.",
            recommendations);
    }

    private static AgentOutputValidation Invalid(string message) =>
        new(
            false,
            message,
            Array.Empty<PersonalStylistRecommendation>());
}
