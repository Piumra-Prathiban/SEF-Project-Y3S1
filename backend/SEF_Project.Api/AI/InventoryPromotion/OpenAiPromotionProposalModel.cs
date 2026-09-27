using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace SEF_Project.Api.AI.InventoryPromotion;

/// <summary>
/// Model-backed proposal boundary for the Inventory &amp; Promotion Agent.
/// It receives only the structured request and results of allow-listed,
/// read-only tools. Its JSON is still parsed strictly, server-priced and
/// checked by <see cref="PromotionProposalValidator"/> before approval.
/// </summary>
public sealed class OpenAiPromotionProposalModel : IPromotionProposalModel
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly InventoryPromotionAgentOptions _options;

    public OpenAiPromotionProposalModel(
        HttpClient httpClient,
        IOptions<InventoryPromotionAgentOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _options.ValidateModelProvider();
    }

    public string Name => $"OpenAI:{_options.Model}";

    public async Task<string> GenerateProposalAsync(
        PromotionAgentContext context,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, _options.Endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            _options.ApiKey);
        request.Content = new StringContent(
            JsonSerializer.Serialize(BuildRequest(context), JsonOptions),
            Encoding.UTF8,
            "application/json");

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"The configured promotion proposal model returned HTTP {(int)response.StatusCode}.");
        }

        await using var responseStream =
            await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(
            responseStream,
            cancellationToken: cancellationToken);

        return ReadOutputText(document.RootElement)
            ?? throw new InvalidDataException(
                "The configured promotion proposal model returned no structured output.");
    }

    private object BuildRequest(PromotionAgentContext context) => new
    {
        model = _options.Model,
        store = false,
        instructions =
            "You are Clothic's Inventory & Promotion Agent. Compare the supplied sales, inventory, "
            + "active-promotion and price evidence and propose only justified promotions. Use only "
            + "supplied product identifiers and names. Do not propose products with a live promotion, "
            + "excluded products, products without safe stock above reorder level, or discounts above "
            + "the supplied limit. Evidence figures must be copied exactly. Return an empty proposals "
            + "array when no promotion is justified. Return only the required structured output.",
        input = JsonSerializer.Serialize(new
        {
            context.Objective,
            focus = context.Focus.ToString(),
            context.AnalysisDays,
            context.MaxProposals,
            context.MaxDiscountPercent,
            nowUtc = context.Now,
            context.ExcludedProductIds,
            context.ReviewerFeedback,
            context.SalesVelocity,
            context.Inventory,
            context.ActivePromotions,
            context.ProductDetails,
            context.ProductPricing
        }, JsonOptions),
        text = new
        {
            format = new
            {
                type = "json_schema",
                name = "inventory_promotion_proposal",
                strict = true,
                schema = BuildOutputSchema()
            }
        }
    };

    private static object BuildOutputSchema() => new
    {
        type = "object",
        additionalProperties = false,
        required = new[] { "schemaVersion", "summary", "proposals" },
        properties = new
        {
            schemaVersion = new { type = "string", @enum = new[] { PromotionAgentConstants.SchemaVersion } },
            summary = new { type = "string" },
            proposals = new
            {
                type = "array",
                items = new
                {
                    type = "object",
                    additionalProperties = false,
                    required = new[]
                    {
                        "productId", "productName", "promotionType", "discountValue",
                        "startDate", "endDate", "rationale", "evidence"
                    },
                    properties = new
                    {
                        productId = new { type = "string" },
                        productName = new { type = "string" },
                        promotionType = new
                        {
                            type = "string",
                            @enum = PromotionAgentConstants.AllowedPromotionTypes.ToArray()
                        },
                        discountValue = new { type = "number" },
                        startDate = new { type = "string" },
                        endDate = new { type = "string" },
                        rationale = new { type = "string" },
                        evidence = new
                        {
                            type = "object",
                            additionalProperties = false,
                            required = new[]
                            {
                                "unitsSold", "previousUnitsSold", "availableQuantity"
                            },
                            properties = new
                            {
                                unitsSold = new { type = "integer" },
                                previousUnitsSold = new { type = "integer" },
                                availableQuantity = new { type = "integer" }
                            }
                        }
                    }
                }
            }
        }
    };

    private static string? ReadOutputText(JsonElement root)
    {
        if (root.TryGetProperty("output_text", out var direct)
            && direct.ValueKind == JsonValueKind.String)
        {
            return direct.GetString();
        }

        if (!root.TryGetProperty("output", out var output)
            || output.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var item in output.EnumerateArray())
        {
            if (!item.TryGetProperty("content", out var content)
                || content.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var part in content.EnumerateArray())
            {
                if (part.TryGetProperty("type", out var type)
                    && type.GetString() == "output_text"
                    && part.TryGetProperty("text", out var text)
                    && text.ValueKind == JsonValueKind.String)
                {
                    return text.GetString();
                }
            }
        }

        return null;
    }
}
