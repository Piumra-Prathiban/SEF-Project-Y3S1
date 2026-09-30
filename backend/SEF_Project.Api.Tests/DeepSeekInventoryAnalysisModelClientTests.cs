using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SEF_Project.Api.AI.DeepSeek;
using SEF_Project.Api.DTOs.AgenticAI;
using SEF_Project.Api.Services.AgenticAI;

namespace SEF_Project.Api.Tests;

public class DeepSeekInventoryAnalysisModelClientTests
{
    [Fact]
    public void Registration_ResolvesInventoryAndSharedDeepSeekClients()
    {
        var services = new ServiceCollection();
        services.Configure<DeepSeekOptions>(_ => { });
        services.AddHttpClient<
            IDeepSeekChatCompletionsClient,
            DeepSeekChatCompletionsClient>();
        services.AddScoped<DeepSeekInventoryAnalysisModelClient>();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        Assert.IsType<DeepSeekInventoryAnalysisModelClient>(
            scope.ServiceProvider.GetRequiredService<
                DeepSeekInventoryAnalysisModelClient>());
        Assert.IsType<DeepSeekChatCompletionsClient>(
            scope.ServiceProvider.GetRequiredService<
                IDeepSeekChatCompletionsClient>());
    }

    [Fact]
    public async Task GenerateRecommendationsAsync_SendsGroundedJsonAndReturnsRawOutput()
    {
        const string modelOutput = "{\"recommendations\":[]}";
        var deepSeekClient = new RecordingDeepSeekClient(modelOutput);
        var client = new DeepSeekInventoryAnalysisModelClient(deepSeekClient);
        var productId = Guid.NewGuid();
        var variantId = Guid.NewGuid();
        using var inventoryResult = JsonDocument.Parse(
            $$"""
            {
              "inventory": [
                {
                  "variantId": "{{variantId}}",
                  "currentStock": 3,
                  "reorderLevel": 5
                }
              ]
            }
            """);
        var request = new InventoryAnalysisRequestDto
        {
            Objective = "Review low-stock variants.",
            ProductIds = new List<Guid> { productId },
            VariantIds = new List<Guid> { variantId }
        };
        using var cancellation = new CancellationTokenSource();

        var result = await client.GenerateRecommendationsAsync(
            request,
            new Dictionary<string, JsonDocument>
            {
                ["GetProductInventory"] = inventoryResult
            },
            cancellation.Token);

        Assert.Same(modelOutput, result);
        Assert.Contains("Inventory Analysis Agent", deepSeekClient.SystemInstruction);
        Assert.Contains("JSON", deepSeekClient.SystemInstruction);
        Assert.Contains("RESTOCK", deepSeekClient.SystemInstruction);
        Assert.Contains("MONITOR", deepSeekClient.SystemInstruction);
        Assert.Contains("NO_ACTION", deepSeekClient.SystemInstruction);
        Assert.Contains("Never invent", deepSeekClient.SystemInstruction);
        Assert.Equal(cancellation.Token, deepSeekClient.CancellationToken);

        using var input = JsonDocument.Parse(deepSeekClient.UserJsonInput);
        var root = input.RootElement;
        Assert.Equal(request.Objective, root.GetProperty("Objective").GetString());
        Assert.Equal(productId, root.GetProperty("ProductIds")[0].GetGuid());
        Assert.Equal(variantId, root.GetProperty("VariantIds")[0].GetGuid());
        Assert.Equal(
            variantId,
            root.GetProperty("toolResults")
                .GetProperty("GetProductInventory")
                .GetProperty("inventory")[0]
                .GetProperty("variantId")
                .GetGuid());
    }

    private sealed class RecordingDeepSeekClient :
        IDeepSeekChatCompletionsClient
    {
        private readonly string _result;

        public RecordingDeepSeekClient(string result)
        {
            _result = result;
        }

        public string SystemInstruction { get; private set; } = string.Empty;

        public string UserJsonInput { get; private set; } = string.Empty;

        public CancellationToken CancellationToken { get; private set; }

        public Task<string> CompleteJsonAsync(
            string systemInstruction,
            string userJsonInput,
            CancellationToken cancellationToken = default)
        {
            SystemInstruction = systemInstruction;
            UserJsonInput = userJsonInput;
            CancellationToken = cancellationToken;
            return Task.FromResult(_result);
        }
    }
}
