namespace SEF_Project.Api.Services.Recommendations;

public class PersonalStylistOutputValidator : IPersonalStylistOutputValidator
{
    public AgentOutputValidation Validate(
        PersonalStylistModelOutput? output,
        PersonalStylistModelInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (output?.Recommendations == null)
        {
            return Invalid(new RecommendationValidationCheck(
                "Schema",
                false,
                "The recommendation model returned no structured recommendation array."));
        }

        var drafts = output.Recommendations;
        var schemaValid =
            drafts.Count <= PersonalStylistAgentContract.MaximumRecommendations &&
            drafts.All(item =>
                item != null &&
                item.ProductId != Guid.Empty &&
                item.VariantId != Guid.Empty &&
                !string.IsNullOrWhiteSpace(item.Reason) &&
                item.Reason.Trim().Length <= 300 &&
                item.Size?.Length <= 50 &&
                item.Colour?.Length <= 50) &&
            drafts.Select(item => item.VariantId).Distinct().Count() == drafts.Count;

        if (!schemaValid)
        {
            return Invalid(new RecommendationValidationCheck(
                "Schema",
                false,
                "The recommendation output does not conform to the expected schema."));
        }

        var products = input.CatalogueProducts
            .GroupBy(item => item.ProductId)
            .ToDictionary(group => group.Key, group => group.First());
        var variants = input.CatalogueProducts
            .SelectMany(product => product.AvailableVariants.Select(variant => new
            {
                product.ProductId,
                Variant = variant
            }))
            .GroupBy(item => item.Variant.Id)
            .ToDictionary(group => group.Key, group => group.First());
        var availability = input.AvailableVariants
            .GroupBy(item => (item.ProductId, item.VariantId))
            .ToDictionary(group => group.Key, group => group.First());

        var checks = new List<RecommendationValidationCheck>
        {
            Pass("Schema", "The recommendation output conforms to the expected schema."),
            Check(
                "ProductExists",
                drafts.All(item => products.ContainsKey(item.ProductId)),
                "Every recommended product exists in the controlled catalogue result.",
                "A recommended product does not exist in the controlled catalogue result."),
            Check(
                "ProductActive",
                drafts.All(item =>
                    products.ContainsKey(item.ProductId) &&
                    availability.ContainsKey((item.ProductId, item.VariantId))),
                "Every recommended product is active.",
                "A recommended product is not active."),
            Check(
                "VariantExists",
                drafts.All(item => variants.ContainsKey(item.VariantId)),
                "Every recommended variant exists in the controlled catalogue result.",
                "A recommended variant does not exist in the controlled catalogue result."),
            Check(
                "VariantOwnership",
                drafts.All(item =>
                    variants.TryGetValue(item.VariantId, out var variant) &&
                    variant.ProductId == item.ProductId),
                "Every recommended variant belongs to its stated product.",
                "A recommended variant does not belong to its stated product."),
            ValidateSize(drafts, input.Preferences.PreferredSize, availability),
            ValidateColour(drafts, input.Preferences.PreferredColours, availability),
            Check(
                "Availability",
                drafts.All(item =>
                    availability.TryGetValue(
                        (item.ProductId, item.VariantId),
                        out var available) &&
                    available.AvailableQuantity > 0),
                "Every recommended variant is active and available.",
                "A recommended variant is inactive or unavailable."),
            Check(
                "Stock",
                drafts.All(item =>
                    item.Quantity > 0 &&
                    availability.TryGetValue(
                        (item.ProductId, item.VariantId),
                        out var available) &&
                    available.AvailableQuantity >= item.Quantity),
                "Required stock exists for every recommended quantity.",
                "A recommended quantity is invalid or exceeds available stock."),
            Check(
                "Price",
                drafts.All(item =>
                    item.Price > 0 &&
                    availability.TryGetValue(
                        (item.ProductId, item.VariantId),
                        out var available) &&
                    available.Price == item.Price),
                "Every recommended price matches authoritative catalogue data.",
                "A recommended price does not match authoritative catalogue data."),
            ValidateBudget(drafts, input.Preferences.Budget, availability)
        };

        if (checks.Any(check => !check.IsValid))
        {
            return new AgentOutputValidation(
                false,
                checks.First(check => !check.IsValid).Message,
                Array.Empty<PersonalStylistRecommendation>(),
                checks);
        }

        var recommendations = drafts.Select(draft =>
        {
            var verified = availability[(draft.ProductId, draft.VariantId)];

            return new PersonalStylistRecommendation(
                verified.ProductId,
                verified.VariantId,
                verified.ProductName,
                verified.VariantName,
                verified.Sku,
                verified.Price,
                draft.Quantity,
                verified.AvailableQuantity,
                verified.Size,
                verified.Colour,
                draft.Reason.Trim());
        }).ToList();

        return new AgentOutputValidation(
            true,
            "All deterministic recommendation checks passed.",
            recommendations,
            checks);
    }

    private static RecommendationValidationCheck ValidateSize(
        IReadOnlyList<PersonalStylistDraftRecommendation> drafts,
        string? requestedSize,
        IReadOnlyDictionary<(Guid ProductId, Guid VariantId), VerifiedProductAvailability>
            availability)
    {
        var supportsSize = availability.Values.Any(item => item.Size != null);
        var valid = drafts.Count == 0 ||
            (requestedSize == null || supportsSize) &&
            drafts.All(item =>
                availability.TryGetValue(
                    (item.ProductId, item.VariantId),
                    out var available) &&
                SameOptional(item.Size, available.Size) &&
                (requestedSize == null || SameOptional(requestedSize, available.Size)));

        return Check(
            "Size",
            valid,
            "Every recommended size is valid for its variant.",
            "A requested or recommended size cannot be verified for its variant.");
    }

    private static RecommendationValidationCheck ValidateColour(
        IReadOnlyList<PersonalStylistDraftRecommendation> drafts,
        IReadOnlyList<string> requestedColours,
        IReadOnlyDictionary<(Guid ProductId, Guid VariantId), VerifiedProductAvailability>
            availability)
    {
        var supportsColour = availability.Values.Any(item => item.Colour != null);
        var valid = drafts.Count == 0 ||
            (requestedColours.Count == 0 || supportsColour) &&
            drafts.All(item =>
                availability.TryGetValue(
                    (item.ProductId, item.VariantId),
                    out var available) &&
                SameOptional(item.Colour, available.Colour) &&
                (requestedColours.Count == 0 ||
                 requestedColours.Contains(
                     available.Colour ?? string.Empty,
                     StringComparer.OrdinalIgnoreCase)));

        return Check(
            "Colour",
            valid,
            "Every recommended colour is valid for its variant.",
            "A requested or recommended colour cannot be verified for its variant.");
    }

    private static RecommendationValidationCheck ValidateBudget(
        IReadOnlyList<PersonalStylistDraftRecommendation> drafts,
        decimal? budget,
        IReadOnlyDictionary<(Guid ProductId, Guid VariantId), VerifiedProductAvailability>
            availability)
    {
        if (!budget.HasValue)
        {
            return Pass("Budget", "No recommendation budget was requested.");
        }

        decimal total = 0;

        foreach (var item in drafts)
        {
            if (item.Quantity <= 0 ||
                !availability.TryGetValue(
                    (item.ProductId, item.VariantId),
                    out var available))
            {
                return Check(
                    "Budget",
                    false,
                    string.Empty,
                    "The recommendation total cannot be verified against the budget.");
            }

            total += available.Price * item.Quantity;
        }

        return Check(
            "Budget",
            total <= budget.Value,
            "The authoritative recommendation total respects the requested budget.",
            "The authoritative recommendation total exceeds the requested budget.");
    }

    private static bool SameOptional(string? claimed, string? authoritative) =>
        string.Equals(
            Normalize(claimed),
            Normalize(authoritative),
            StringComparison.OrdinalIgnoreCase);

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static RecommendationValidationCheck Pass(string rule, string message) =>
        new(rule, true, message);

    private static RecommendationValidationCheck Check(
        string rule,
        bool isValid,
        string validMessage,
        string invalidMessage) =>
        new(rule, isValid, isValid ? validMessage : invalidMessage);

    private static AgentOutputValidation Invalid(
        RecommendationValidationCheck check) =>
        new(
            false,
            check.Message,
            Array.Empty<PersonalStylistRecommendation>(),
            new[] { check });
}
