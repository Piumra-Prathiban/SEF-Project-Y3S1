using System.Text.Json;
using SEF_Project.Api.AI.DeepSeek;

namespace SEF_Project.Api.Services.Recommendations;

/// <summary>
/// DeepSeek-backed reasoning boundary for the Personal Stylist Agent. The
/// returned recommendations remain untrusted until the agent validates them
/// against the supplied catalogue and availability data.
/// </summary>
public sealed class DeepSeekPersonalStylistModel :
    IPersonalStylistRecommendationModel
{
    private const string SystemInstruction = """
        You are the Clothic Personal Stylist. Use only the supplied JSON data and return one JSON object with this exact shape:
        {
          "recommendations": [
            {
              "productId": "GUID",
              "variantId": "GUID",
              "price": 0.0,
              "quantity": 1,
              "size": null,
              "colour": null,
              "reason": "string"
            }
          ]
        }
        productId and variantId must be GUID strings, price must be a number, quantity must be an integer, size and colour must each be a string or null, and reason must be a string.
        Select at most five items. Select only an exact productId and variantId pair present in availableVariants; a variant appearing only in catalogueProducts is not eligible.
        Copy each selected variant's price, size, and colour exactly from the supplied data.
        When preferredSize is present, every selected available variant must match it. When preferredColours is non-empty, every selected available variant must match one of those colours.
        Keep the total price, calculated as price multiplied by quantity for every item, within the supplied budget when a budget is present.
        Never request a quantity greater than the selected variant's available quantity.
        If no available variant satisfies the requested size, colour, stock, price, and budget constraints, return {"recommendations":[]}.
        Prefer wishlist items marked as available when they suit the customer's preferences.
        Give each item a concise customer-facing reason.
        Return JSON only, with no markdown or explanatory text outside the JSON object.
        """;

    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    private readonly IDeepSeekChatCompletionsClient _client;

    public DeepSeekPersonalStylistModel(
        IDeepSeekChatCompletionsClient client)
    {
        _client = client;
    }

    public async Task<PersonalStylistModelOutput> GenerateAsync(
        PersonalStylistModelInput input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        var inputJson = JsonSerializer.Serialize(input, JsonOptions);
        var outputText = await _client.CompleteJsonAsync(
            SystemInstruction,
            inputJson,
            cancellationToken);

        if (string.IsNullOrWhiteSpace(outputText))
        {
            throw new InvalidDataException(
                "DeepSeek returned no personal stylist JSON output.");
        }

        try
        {
            return JsonSerializer.Deserialize<PersonalStylistModelOutput>(
                    outputText,
                    JsonOptions)
                ?? throw new InvalidDataException(
                    "DeepSeek personal stylist output could not be read.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                "DeepSeek personal stylist output could not be read.",
                exception);
        }
    }
}
