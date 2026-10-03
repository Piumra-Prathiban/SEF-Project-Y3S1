using System.Text.Json;
using System.Text.Json.Nodes;
using SEF_Project.Api.AI.DeepSeek;
using SEF_Project.Api.DTOs.AgenticAI;

namespace SEF_Project.Api.Services.AgenticAI;

public sealed class DeepSeekInventoryAnalysisModelClient :
    IInventoryAnalysisModelClient
{
    private const string SystemInstruction = """
        You are the Inventory Analysis Agent for a fashion commerce platform.
        Analyze only the supplied request and tool data, then return one JSON object with this exact shape:
        {
          "recommendations": [
            {
              "variantId": "string GUID",
              "sku": "string",
              "productName": "string",
              "currentStock": 0,
              "reorderLevel": 0,
              "recommendedAction": "RESTOCK | MONITOR | NO_ACTION",
              "recommendedQuantity": 0,
              "reason": "string"
            }
          ]
        }
        Use only variants, SKUs, product names, current stock values, and reorder levels present in the supplied toolResults JSON.
        Never invent a variant, product, SKU, stock number, reorder level, or other catalogue fact.
        recommendedAction must be exactly RESTOCK, MONITOR, or NO_ACTION.
        Return JSON only, with no markdown or explanatory text outside the JSON object.
        """;

    private readonly IDeepSeekChatCompletionsClient _client;

    public DeepSeekInventoryAnalysisModelClient(
        IDeepSeekChatCompletionsClient client)
    {
        _client = client;
    }

    public Task<string> GenerateRecommendationsAsync(
        InventoryAnalysisRequestDto request,
        IReadOnlyDictionary<string, JsonDocument> toolResults,
        CancellationToken cancellationToken = default)
    {
        var toolResultObject = new JsonObject();

        foreach (var (toolName, result) in toolResults)
        {
            toolResultObject[toolName] = JsonNode.Parse(
                result.RootElement.GetRawText());
        }

        var inputJson = JsonSerializer.Serialize(new
        {
            request.Objective,
            request.ProductIds,
            request.VariantIds,
            toolResults = toolResultObject
        });

        return _client.CompleteJsonAsync(
            SystemInstruction,
            inputJson,
            cancellationToken);
    }
}
