using System.Text.Json;
using SEF_Project.Api.AI.DeepSeek;

namespace SEF_Project.Api.AI.InventoryPromotion;

/// <summary>
/// DeepSeek-backed proposal model. Builds the request, calls the shared
/// client, and returns the model's JSON text unchanged; the strict parser and
/// deterministic validator decide whether it is acceptable.
/// </summary>
public sealed class DeepSeekPromotionProposalModel : IPromotionProposalModel
{
    private const string Instructions = """
        You are the Clothic Inventory & Promotion Agent. Propose bounded, justified product promotions from the supplied data only.

        Respond with a single JSON object and nothing else, in exactly this shape:
        {
          "schemaVersion": "1.0",
          "summary": "short summary of the proposals",
          "proposals": [
            {
              "productId": "guid from the supplied data",
              "productName": "name from the supplied data",
              "promotionType": "PercentageDiscount" or "FixedAmountDiscount",
              "discountValue": number,
              "startDate": "ISO 8601 UTC date-time",
              "endDate": "ISO 8601 UTC date-time",
              "rationale": "short factual justification citing the evidence",
              "evidence": { "unitsSold": int, "previousUnitsSold": int, "availableQuantity": int }
            }
          ]
        }

        Rules:
        - Propose only justified promotions, using only product ids present in the supplied data.
        - Skip products that already have a live promotion, products in excludedProductIds, and products without safe stock (available quantity must exceed the reorder level).
        - Keep every discount within maxDiscountPercent and the number of proposals within maxProposals.
        - Start dates must be after "now"; keep promotions short.
        - Copy the evidence figures exactly from the supplied tool results; never invent numbers.
        - If reviewerFeedback is present, revise the proposals to address it.
        - If nothing is justified, return an empty proposals array.
        """;

    private readonly IDeepSeekChatCompletionsClient _client;

    public DeepSeekPromotionProposalModel(IDeepSeekChatCompletionsClient client)
    {
        _client = client;
    }

    public string Name => "DeepSeek";

    public Task<string> GenerateProposalAsync(PromotionAgentContext context, CancellationToken cancellationToken)
    {
        var input = new
        {
            objective = context.Objective,
            focus = context.Focus.ToString(),
            analysisDays = context.AnalysisDays,
            maxProposals = context.MaxProposals,
            maxDiscountPercent = context.MaxDiscountPercent,
            now = context.Now,
            excludedProductIds = context.ExcludedProductIds,
            reviewerFeedback = context.ReviewerFeedback,
            salesVelocity = context.SalesVelocity,
            inventory = context.Inventory,
            activePromotions = context.ActivePromotions,
            productDetails = context.ProductDetails,
            productPricing = context.ProductPricing
        };

        return _client.CompleteJsonAsync(
            Instructions,
            JsonSerializer.Serialize(input, AgentJson.Options),
            cancellationToken);
    }
}
