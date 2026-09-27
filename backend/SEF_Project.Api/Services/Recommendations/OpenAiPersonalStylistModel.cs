using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace SEF_Project.Api.Services.Recommendations;

/// <summary>
/// Model-backed reasoning boundary for the Personal Stylist Agent. The model
/// receives only structured results from allow-listed read-only tools. Its
/// output remains untrusted until PersonalStylistOutputValidator checks every
/// identifier, price, option, stock quantity, and budget against server facts.
/// </summary>
public sealed class OpenAiPersonalStylistModel : IPersonalStylistRecommendationModel
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _httpClient;
    private readonly PersonalStylistAgentOptions _options;

    public OpenAiPersonalStylistModel(
        HttpClient httpClient,
        IOptions<PersonalStylistAgentOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _options.Validate();
    }

    public async Task<PersonalStylistModelOutput> GenerateAsync(
        PersonalStylistModelInput input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        using var request = new HttpRequestMessage(HttpMethod.Post, _options.Endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            _options.ApiKey);
        request.Content = new StringContent(
            JsonSerializer.Serialize(BuildRequest(input), JsonOptions),
            Encoding.UTF8,
            "application/json");

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"The configured recommendation model returned HTTP {(int)response.StatusCode}.");
        }

        await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(
            responseStream,
            cancellationToken: cancellationToken);
        var outputText = ReadOutputText(document.RootElement)
            ?? throw new InvalidDataException(
                "The configured recommendation model returned no structured output.");

        return JsonSerializer.Deserialize<PersonalStylistModelOutput>(outputText, JsonOptions)
            ?? throw new InvalidDataException(
                "The configured recommendation model output could not be read.");
    }

    private object BuildRequest(PersonalStylistModelInput input) => new
    {
        model = _options.Model,
        instructions =
            "You are Clothic's Personal Stylist Agent. Select at most five cohesive fashion variants " +
            "for the requested occasion and preferences. Use only the supplied productId and variantId " +
            "values and copy their authoritative price, size and colour exactly. Stay within the total " +
            "budget, never exceed available quantity, prefer available wishlist products when suitable, " +
            "and give a concise customer-facing reason. Return only the required structured output.",
        input = JsonSerializer.Serialize(new
        {
            preferences = input.Preferences,
            wishlistItems = input.WishlistItems,
            catalogueProducts = input.CatalogueProducts,
            availableVariants = input.AvailableVariants,
            relaxedCriteria = input.RelaxedCriteria
        }, JsonOptions),
        text = new
        {
            format = new
            {
                type = "json_schema",
                name = "personal_stylist_recommendations",
                strict = true,
                schema = BuildOutputSchema()
            }
        }
    };

    private static object BuildOutputSchema() => new
    {
        type = "object",
        additionalProperties = false,
        required = new[] { "recommendations" },
        properties = new
        {
            recommendations = new
            {
                type = "array",
                maxItems = PersonalStylistAgentContract.MaximumRecommendations,
                items = new
                {
                    type = "object",
                    additionalProperties = false,
                    required = new[]
                    {
                        "productId", "variantId", "price", "quantity",
                        "size", "colour", "reason"
                    },
                    properties = new
                    {
                        productId = new { type = "string", format = "uuid" },
                        variantId = new { type = "string", format = "uuid" },
                        price = new { type = "number", minimum = 0 },
                        quantity = new { type = "integer", minimum = 1 },
                        size = new { type = new[] { "string", "null" } },
                        colour = new { type = new[] { "string", "null" } },
                        reason = new { type = "string", minLength = 10, maxLength = 500 }
                    }
                }
            }
        }
    };

    private static string? ReadOutputText(JsonElement root)
    {
        if (root.TryGetProperty("output_text", out var direct) &&
            direct.ValueKind == JsonValueKind.String)
        {
            return direct.GetString();
        }

        if (!root.TryGetProperty("output", out var output) ||
            output.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var item in output.EnumerateArray())
        {
            if (!item.TryGetProperty("content", out var content) ||
                content.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var part in content.EnumerateArray())
            {
                if (part.TryGetProperty("type", out var type) &&
                    type.GetString() == "output_text" &&
                    part.TryGetProperty("text", out var text) &&
                    text.ValueKind == JsonValueKind.String)
                {
                    return text.GetString();
                }
            }
        }

        return null;
    }
}
