using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using SEF_Project.Api.AI.DeepSeek;
using SEF_Project.Api.AI.InventoryPromotion;
using SEF_Project.Api.DTOs.Agents;

namespace SEF_Project.Api.Tests;

public class DeepSeekPromotionProposalModelTests : IClassFixture<MarketingApiFactory>
{
    private readonly MarketingApiFactory _factory;

    public DeepSeekPromotionProposalModelTests(MarketingApiFactory factory)
    {
        _factory = factory;
    }

    private const string FixedOutput =
        "{\"schemaVersion\":\"1.0\",\"summary\":\"One promotion.\",\"proposals\":[{" +
        "\"productId\":\"11111111-1111-1111-1111-111111111111\",\"productName\":\"Linen Shirt\"," +
        "\"promotionType\":\"PercentageDiscount\",\"discountValue\":15," +
        "\"startDate\":\"2026-10-16T00:00:00Z\",\"endDate\":\"2026-10-30T00:00:00Z\"," +
        "\"rationale\":\"Sales fell.\",\"evidence\":{\"unitsSold\":4,\"previousUnitsSold\":10,\"availableQuantity\":40}}]}";

    [Fact]
    public async Task GenerateProposalAsync_SendsContextAndReturnsClientOutputUnchanged()
    {
        var client = new CapturingClient(FixedOutput);
        var model = new DeepSeekPromotionProposalModel(client);

        var result = await model.GenerateProposalAsync(Context(), default);

        Assert.Equal(FixedOutput, result);
        Assert.Contains("JSON", client.SystemInstruction);
        Assert.Contains("proposals", client.SystemInstruction);
        Assert.Contains("Find a justified promotion for declining products.", client.UserJsonInput);
        Assert.Contains("SalesVelocityMarker", client.UserJsonInput);
        Assert.Contains("InventoryMarker", client.UserJsonInput);
        Assert.Contains("ActivePromotionsMarker", client.UserJsonInput);
        Assert.Contains("ProductDetailsMarker", client.UserJsonInput);
        Assert.Contains("ProductPricingMarker", client.UserJsonInput);
    }

    [Theory]
    [InlineData("Local", typeof(LocalPromotionProposalModel))]
    [InlineData("local", typeof(LocalPromotionProposalModel))]
    [InlineData("DeepSeek", typeof(DeepSeekPromotionProposalModel))]
    [InlineData("deepseek", typeof(DeepSeekPromotionProposalModel))]
    public void ProviderSetting_SelectsTheRegisteredPromotionModel(string provider, Type expected)
    {
        // Real Program.cs factory; only the (not yet registered) shared client is faked.
        using var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("InventoryPromotionAgent:Provider", provider);
            builder.ConfigureServices(services =>
                services.AddScoped<IDeepSeekChatCompletionsClient>(_ => new CapturingClient(FixedOutput)));
        });

        using var scope = factory.Services.CreateScope();
        var model = scope.ServiceProvider.GetRequiredService<IPromotionProposalModel>();

        Assert.IsType(expected, model);
    }

    private static PromotionAgentContext Context() => new(
        "Find a justified promotion for declining products.",
        PromotionAgentFocus.DecliningSales,
        30,
        3,
        20,
        new DateTime(2026, 10, 15, 12, 0, 0, DateTimeKind.Utc),
        Array.Empty<Guid>(),
        null,
        Node("SalesVelocityMarker"),
        Node("InventoryMarker"),
        Node("ActivePromotionsMarker"),
        Node("ProductDetailsMarker"),
        Node("ProductPricingMarker"));

    private static JsonNode Node(string marker) => new JsonObject { ["marker"] = marker };

    private sealed class CapturingClient : IDeepSeekChatCompletionsClient
    {
        private readonly string _output;

        public CapturingClient(string output) => _output = output;

        public string SystemInstruction { get; private set; } = string.Empty;

        public string UserJsonInput { get; private set; } = string.Empty;

        public Task<string> CompleteJsonAsync(
            string systemInstruction,
            string userJsonInput,
            CancellationToken cancellationToken = default)
        {
            SystemInstruction = systemInstruction;
            UserJsonInput = userJsonInput;
            return Task.FromResult(_output);
        }
    }
}
