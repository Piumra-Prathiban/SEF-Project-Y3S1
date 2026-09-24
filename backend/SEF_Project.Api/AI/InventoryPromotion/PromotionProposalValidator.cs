using System.Text.Json;
using System.Text.Json.Nodes;
using SEF_Project.Api.DTOs.Agents;

namespace SEF_Project.Api.AI.InventoryPromotion;

public sealed record ValidationOutcome(string Rule, bool IsValid, string Message);

public sealed record ProposalValidationInput(
    IReadOnlyDictionary<Guid, ProductFacts> Facts,
    IReadOnlyDictionary<Guid, JsonNode?> ServerPricing,
    int MaxProposals,
    int MaxDiscountPercent,
    int MaxPromotionDays,
    DateTime Now,
    IReadOnlyCollection<Guid> ExcludedProductIds);

/// <summary>
/// Deterministic checks on model output. Nothing is repaired: any failure
/// rejects the whole proposal (fail closed).
/// </summary>
public static class PromotionProposalValidator
{
    public const int HardMaxProposals = 10;

    /// <summary>Strict schema parsing of the raw model output.</summary>
    public static PromotionProposalDocument Parse(string? raw, int maxCharacters)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            throw new ProposalSchemaException("The model returned no output.");
        }

        if (raw.Length > maxCharacters)
        {
            throw new ProposalSchemaException("The model output is too long.");
        }

        PromotionProposalDocument? document;
        try
        {
            document = JsonSerializer.Deserialize<PromotionProposalDocument>(raw, AgentJson.StrictOptions);
        }
        catch (JsonException)
        {
            throw new ProposalSchemaException("The model output is not a valid proposal JSON document.");
        }

        if (document is null)
        {
            throw new ProposalSchemaException("The model output is empty.");
        }

        if (document.SchemaVersion != PromotionAgentConstants.SchemaVersion)
        {
            throw new ProposalSchemaException($"Unsupported schemaVersion; expected '{PromotionAgentConstants.SchemaVersion}'.");
        }

        if (document.Proposals is null || document.Proposals.Count > HardMaxProposals)
        {
            throw new ProposalSchemaException($"'proposals' must be a list of at most {HardMaxProposals} items.");
        }

        if (document.Summary is null || document.Summary.Length > 1000)
        {
            throw new ProposalSchemaException("'summary' is required and must be at most 1000 characters.");
        }

        foreach (var proposal in document.Proposals)
        {
            if (proposal is null
                || proposal.ProductId == Guid.Empty
                || proposal.Evidence is null
                || proposal.StartDate == default
                || proposal.EndDate == default
                || proposal.ProductName is null || proposal.ProductName.Length > 200
                || proposal.Rationale is null || proposal.Rationale.Length is < 10 or > 500
                || !PromotionAgentConstants.AllowedPromotionTypes.Contains(proposal.PromotionType ?? string.Empty))
            {
                throw new ProposalSchemaException(
                    "Each proposal needs productId, productName, an allowed promotionType, "
                    + "startDate, endDate, a 10–500 character rationale and evidence.");
            }

            proposal.StartDate = ToUtc(proposal.StartDate);
            proposal.EndDate = ToUtc(proposal.EndDate);
        }

        return document;
    }

    public static List<ValidationOutcome> Validate(PromotionProposalDocument document, ProposalValidationInput input)
    {
        var results = new List<ValidationOutcome>();
        var proposals = document.Proposals;

        var distinct = proposals.Select(p => p.ProductId).Distinct().Count() == proposals.Count;
        results.Add(new ValidationOutcome(
            "ProposalLimit",
            proposals.Count <= input.MaxProposals && distinct,
            proposals.Count > input.MaxProposals
                ? $"{proposals.Count} proposals exceed the limit of {input.MaxProposals}."
                : distinct ? $"{proposals.Count} proposal(s) within the limit." : "A product is proposed more than once."));

        foreach (var proposal in proposals)
        {
            var label = string.IsNullOrWhiteSpace(proposal.ProductName) ? proposal.ProductId.ToString() : proposal.ProductName;

            if (!input.Facts.TryGetValue(proposal.ProductId, out var facts) || !facts.Exists)
            {
                results.Add(new ValidationOutcome("ProductExists", false,
                    $"{label}: product does not exist or is inactive."));
                continue;
            }

            results.Add(new ValidationOutcome("ProductExists", true, $"{label}: active product found."));

            results.Add(input.ExcludedProductIds.Contains(proposal.ProductId)
                ? new ValidationOutcome("ReviewerConstraints", false, $"{label}: the reviewer excluded this product.")
                : new ValidationOutcome("ReviewerConstraints", true, $"{label}: not excluded by the reviewer."));

            results.Add(facts.AvailableQuantity > facts.ReorderLevel && facts.AvailableQuantity > 0
                ? new ValidationOutcome("SufficientInventory", true,
                    $"{label}: {facts.AvailableQuantity} available, above the reorder level of {facts.ReorderLevel}.")
                : new ValidationOutcome("SufficientInventory", false,
                    $"{label}: only {facts.AvailableQuantity} available (reorder level {facts.ReorderLevel}); a promotion could cause a stock-out."));

            results.Add(CheckDiscount(proposal, facts, input.MaxDiscountPercent, label));
            results.Add(CheckDates(proposal, input, label));

            results.Add(facts.HasActivePromotion
                ? new ValidationOutcome("NoConflict", false, $"{label}: already has a live promotion.")
                : new ValidationOutcome("NoConflict", true, $"{label}: no live promotion."));

            var evidence = proposal.Evidence;
            var evidenceMatches = evidence.UnitsSold == facts.UnitsSold
                && evidence.PreviousUnitsSold == facts.PreviousUnitsSold
                && evidence.AvailableQuantity == facts.AvailableQuantity;
            results.Add(new ValidationOutcome("EvidenceMatchesData", evidenceMatches, evidenceMatches
                ? $"{label}: evidence matches tool data."
                : $"{label}: evidence (sold {evidence.UnitsSold}, previous {evidence.PreviousUnitsSold}, available {evidence.AvailableQuantity}) "
                  + $"does not match tool data (sold {facts.UnitsSold}, previous {facts.PreviousUnitsSold}, available {facts.AvailableQuantity})."));

            results.Add(CheckPricing(proposal, input, label));
        }

        return results;
    }

    /// <summary>High impact: a large discount or many promotions at once.</summary>
    public static PromotionImpactLevel AssessImpact(
        PromotionProposalDocument document,
        IReadOnlyDictionary<Guid, ProductFacts> facts,
        InventoryPromotionAgentOptions options)
    {
        if (document.Proposals.Count > options.HighImpactProposalCount)
        {
            return PromotionImpactLevel.High;
        }

        return document.Proposals.Any(p => EffectivePercent(p, facts) >= options.HighImpactDiscountPercent)
            ? PromotionImpactLevel.High
            : PromotionImpactLevel.Low;
    }

    private static decimal EffectivePercent(PromotionProposalItem proposal, IReadOnlyDictionary<Guid, ProductFacts> facts)
    {
        if (proposal.PromotionType == "PercentageDiscount")
        {
            return proposal.DiscountValue;
        }

        return facts.TryGetValue(proposal.ProductId, out var f) && f.MinPrice > 0
            ? proposal.DiscountValue / f.MinPrice * 100m
            : 100m;
    }

    private static ValidationOutcome CheckDiscount(PromotionProposalItem proposal, ProductFacts facts, int maxPercent, string label)
    {
        var percent = EffectivePercent(proposal, new Dictionary<Guid, ProductFacts> { [facts.ProductId] = facts });
        var valid = proposal.DiscountValue > 0 && percent <= maxPercent;

        return new ValidationOutcome("DiscountLimits", valid, valid
            ? $"{label}: {percent:0.##}% discount is within the {maxPercent}% limit."
            : $"{label}: {percent:0.##}% discount is outside the allowed range (0, {maxPercent}%].");
    }

    private static ValidationOutcome CheckDates(PromotionProposalItem proposal, ProposalValidationInput input, string label)
    {
        var today = DateTime.SpecifyKind(input.Now.Date, DateTimeKind.Utc);
        var days = (proposal.EndDate - proposal.StartDate).TotalDays;

        if (proposal.StartDate < today)
        {
            return new ValidationOutcome("PromotionDates", false, $"{label}: start date is in the past.");
        }

        if (days < 1 || days > input.MaxPromotionDays)
        {
            return new ValidationOutcome("PromotionDates", false,
                $"{label}: promotion must last 1 to {input.MaxPromotionDays} days.");
        }

        return new ValidationOutcome("PromotionDates", true, $"{label}: runs for {days:0} days.");
    }

    private static ValidationOutcome CheckPricing(PromotionProposalItem proposal, ProposalValidationInput input, string label)
    {
        if (!input.ServerPricing.TryGetValue(proposal.ProductId, out var pricing) || pricing is null)
        {
            return new ValidationOutcome("ServerPricing", false, $"{label}: the server could not price this promotion.");
        }

        var variants = pricing["variants"]?.AsArray() ?? new JsonArray();
        var prices = variants
            .Select(v => (Final: ReadDecimal(v?["finalPrice"]), Discount: ReadDecimal(v?["discountAmount"])))
            .ToList();

        var valid = prices.Count > 0 && prices.All(p => p.Final > 0 && p.Discount > 0);

        return new ValidationOutcome("ServerPricing", valid, valid
            ? $"{label}: server priced {prices.Count} variant(s); every final price stays above zero."
            : $"{label}: the discount would make an item free or does not reduce the price.");
    }

    private static decimal ReadDecimal(JsonNode? node)
    {
        try
        {
            return node?.Deserialize<decimal>(AgentJson.Options) ?? 0m;
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException)
        {
            return 0m;
        }
    }

    private static DateTime ToUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };
}
