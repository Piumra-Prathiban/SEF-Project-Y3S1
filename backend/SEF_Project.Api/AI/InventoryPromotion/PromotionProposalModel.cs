using System.Text.Json;
using System.Text.Json.Nodes;
using SEF_Project.Api.DTOs.Agents;

namespace SEF_Project.Api.AI.InventoryPromotion;

/// <summary>
/// Everything the proposal model may see: the structured request and the
/// results of the allow-listed tools. No database, services or secrets.
/// </summary>
public sealed record PromotionAgentContext(
    string Objective,
    PromotionAgentFocus Focus,
    int AnalysisDays,
    int MaxProposals,
    int MaxDiscountPercent,
    DateTime Now,
    IReadOnlyCollection<Guid> ExcludedProductIds,
    string? ReviewerFeedback,
    JsonNode SalesVelocity,
    JsonNode Inventory,
    JsonNode ActivePromotions,
    JsonNode ProductDetails,
    JsonNode ProductPricing);

/// <summary>
/// Replaceable model boundary. Implementations return raw text that must be a
/// <see cref="PromotionProposalDocument"/> JSON document; it is parsed
/// strictly and validated deterministically before use. An LLM-backed
/// implementation can be registered instead of the local policy without any
/// other change.
/// </summary>
public interface IPromotionProposalModel
{
    string Name { get; }

    Task<string> GenerateProposalAsync(PromotionAgentContext context, CancellationToken cancellationToken);
}

/// <summary>Per-product facts derived only from tool results.</summary>
public sealed record ProductFacts(
    Guid ProductId,
    string ProductName,
    bool Exists,
    bool HasActivePromotion,
    int UnitsSold,
    int PreviousUnitsSold,
    int AvailableQuantity,
    int ReorderLevel,
    decimal MinPrice);

public static class PromotionAgentData
{
    private sealed record VelocityItem(Guid ProductId, string ProductName, int UnitsSold, int PreviousUnitsSold);
    private sealed record InventoryItem(Guid ProductId, bool IsActive, int AvailableQuantity, int ReorderLevel);
    private sealed record DetailItem(Guid ProductId, string ProductName, bool HasActivePromotion);
    private sealed record PriceVariant(decimal Price);
    private sealed record PriceItem(Guid ProductId, List<PriceVariant> Variants);
    private sealed record LivePromotion(Guid Id, List<Guid> ProductIds);
    private sealed record ItemsOf<T>(List<T> Items);
    private sealed record ProductsOf<T>(List<T> Products);

    public static Dictionary<Guid, ProductFacts> Build(
        JsonNode salesVelocity,
        JsonNode inventory,
        JsonNode activePromotions,
        JsonNode productDetails,
        JsonNode productPricing)
    {
        var velocity = Read<ItemsOf<VelocityItem>>(salesVelocity).Items;
        var stock = Read<ItemsOf<InventoryItem>>(inventory).Items;
        var live = Read<ItemsOf<LivePromotion>>(activePromotions).Items;
        var details = Read<ProductsOf<DetailItem>>(productDetails).Products.ToDictionary(d => d.ProductId);
        var prices = Read<ProductsOf<PriceItem>>(productPricing).Products.ToDictionary(p => p.ProductId);

        var productIds = velocity.Select(v => v.ProductId).Concat(details.Keys).Distinct();

        return productIds.ToDictionary(id => id, id =>
        {
            var sales = velocity.Where(v => v.ProductId == id).ToList();
            var activeStock = stock.Where(s => s.ProductId == id && s.IsActive).ToList();
            var exists = details.TryGetValue(id, out var detail);
            var variantPrices = prices.TryGetValue(id, out var price)
                ? price.Variants.Select(v => v.Price).ToList()
                : new List<decimal>();

            return new ProductFacts(
                ProductId: id,
                ProductName: detail?.ProductName ?? sales.FirstOrDefault()?.ProductName ?? string.Empty,
                Exists: exists,
                HasActivePromotion: (detail?.HasActivePromotion ?? false) || live.Any(p => p.ProductIds.Contains(id)),
                UnitsSold: sales.Sum(s => s.UnitsSold),
                PreviousUnitsSold: sales.Sum(s => s.PreviousUnitsSold),
                AvailableQuantity: activeStock.Sum(s => s.AvailableQuantity),
                ReorderLevel: activeStock.Sum(s => s.ReorderLevel),
                MinPrice: variantPrices.Count == 0 ? 0m : variantPrices.Min());
        });
    }

    private static T Read<T>(JsonNode node) =>
        node.Deserialize<T>(AgentJson.Options)
        ?? throw new ToolRejectedException("Tool result could not be read.");
}

/// <summary>
/// Default model: a deterministic, data-grounded promotion policy.
/// <list type="bullet">
/// <item>DecliningSales: units fell by 10%+ vs the previous period.</item>
/// <item>SlowMoving: no sales with stock, or more than 60 days of cover.</item>
/// </list>
/// Skips products that already have a live promotion, are excluded by the
/// reviewer, or do not have stock above their reorder level.
/// </summary>
public sealed class LocalPromotionProposalModel : IPromotionProposalModel
{
    private const int PromotionDays = 14;
    private const decimal SlowMovingCoverDays = 60m;

    public string Name => "LocalPromotionPolicy";

    public Task<string> GenerateProposalAsync(PromotionAgentContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var facts = PromotionAgentData.Build(
            context.SalesVelocity,
            context.Inventory,
            context.ActivePromotions,
            context.ProductDetails,
            context.ProductPricing);

        var eligible = facts.Values
            .Where(f => f.Exists
                && !f.HasActivePromotion
                && !context.ExcludedProductIds.Contains(f.ProductId)
                && f.AvailableQuantity > f.ReorderLevel
                && f.MinPrice > 0);

        var start = DateTime.SpecifyKind(context.Now.Date.AddDays(1), DateTimeKind.Utc);
        var end = start.AddDays(PromotionDays);

        var proposals = (context.Focus == PromotionAgentFocus.DecliningSales
                ? DecliningSales(eligible, context, start, end)
                : SlowMoving(eligible, context, start, end))
            .Take(context.MaxProposals)
            .ToList();

        var document = new PromotionProposalDocument
        {
            SchemaVersion = PromotionAgentConstants.SchemaVersion,
            Summary = proposals.Count == 0
                ? "No products met the promotion criteria."
                : $"{proposals.Count} promotion proposal(s) for {(context.Focus == PromotionAgentFocus.DecliningSales ? "declining" : "slow-moving")} products.",
            Proposals = proposals
        };

        return Task.FromResult(JsonSerializer.Serialize(document, AgentJson.Options));
    }

    private static IEnumerable<PromotionProposalItem> DecliningSales(
        IEnumerable<ProductFacts> products,
        PromotionAgentContext context,
        DateTime start,
        DateTime end)
    {
        return products
            .Where(f => f.PreviousUnitsSold > 0 && f.UnitsSold * 100 <= f.PreviousUnitsSold * 90)
            .Select(f => (Facts: f, DeclinePercent: (f.PreviousUnitsSold - f.UnitsSold) * 100m / f.PreviousUnitsSold))
            .OrderByDescending(x => x.DeclinePercent)
            .ThenBy(x => x.Facts.ProductName, StringComparer.Ordinal)
            .Select(x => Proposal(
                x.Facts,
                x.DeclinePercent >= 50 ? 20 : x.DeclinePercent >= 25 ? 15 : 10,
                context,
                start,
                end,
                $"Units sold fell from {x.Facts.PreviousUnitsSold} to {x.Facts.UnitsSold} "
                + $"({x.DeclinePercent:0.#}% lower) over the last {context.AnalysisDays} days "
                + $"while {x.Facts.AvailableQuantity} units are available."));
    }

    private static IEnumerable<PromotionProposalItem> SlowMoving(
        IEnumerable<ProductFacts> products,
        PromotionAgentContext context,
        DateTime start,
        DateTime end)
    {
        return products
            .Select(f => (Facts: f, Cover: f.UnitsSold == 0
                ? decimal.MaxValue
                : f.AvailableQuantity / (f.UnitsSold / (decimal)context.AnalysisDays)))
            .Where(x => x.Cover > SlowMovingCoverDays)
            .OrderByDescending(x => x.Cover)
            .ThenBy(x => x.Facts.ProductName, StringComparer.Ordinal)
            .Select(x => Proposal(
                x.Facts,
                x.Facts.UnitsSold == 0 ? 15 : 10,
                context,
                start,
                end,
                x.Facts.UnitsSold == 0
                    ? $"No units sold in the last {context.AnalysisDays} days while {x.Facts.AvailableQuantity} units are available."
                    : $"Only {x.Facts.UnitsSold} units sold in the last {context.AnalysisDays} days against "
                      + $"{x.Facts.AvailableQuantity} available (about {x.Cover:0} days of cover)."));
    }

    private static PromotionProposalItem Proposal(
        ProductFacts facts,
        int discountPercent,
        PromotionAgentContext context,
        DateTime start,
        DateTime end,
        string rationale) =>
        new()
        {
            ProductId = facts.ProductId,
            ProductName = facts.ProductName,
            PromotionType = "PercentageDiscount",
            DiscountValue = Math.Min(discountPercent, context.MaxDiscountPercent),
            StartDate = start,
            EndDate = end,
            Rationale = rationale,
            Evidence = new ProposalEvidence
            {
                UnitsSold = facts.UnitsSold,
                PreviousUnitsSold = facts.PreviousUnitsSold,
                AvailableQuantity = facts.AvailableQuantity
            }
        };
}
