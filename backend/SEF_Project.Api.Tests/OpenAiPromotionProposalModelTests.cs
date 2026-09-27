using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using SEF_Project.Api.AI.InventoryPromotion;
using SEF_Project.Api.DTOs.Agents;

namespace SEF_Project.Api.Tests;

public class OpenAiPromotionProposalModelTests
{
    [Fact]
    public async Task GenerateProposalAsync_SendsOnlyStructuredContextAndReturnsStructuredText()
    {
        var productId = Guid.NewGuid();
        var proposal = JsonSerializer.Serialize(new
        {
            schemaVersion = "1.0",
            summary = "One grounded proposal.",
            proposals = new[]
            {
                new
                {
                    productId,
                    productName = "Linen Shirt",
                    promotionType = "PercentageDiscount",
                    discountValue = 10,
                    startDate = "2026-10-16T00:00:00Z",
                    endDate = "2026-10-30T00:00:00Z",
                    rationale = "Sales declined while stock remains healthy.",
                    evidence = new
                    {
                        unitsSold = 2,
                        previousUnitsSold = 5,
                        availableQuantity = 20
                    }
                }
            }
        });
        var handler = new RecordingHandler(JsonSerializer.Serialize(new
        {
            output = new[]
            {
                new
                {
                    content = new[] { new { type = "output_text", text = proposal } }
                }
            }
        }));
        var model = new OpenAiPromotionProposalModel(
            new HttpClient(handler),
            Options.Create(OpenAiOptions()));

        var result = await model.GenerateProposalAsync(Context(productId), default);

        Assert.Equal(proposal, result);
        Assert.Equal("Bearer", handler.AuthorizationScheme);
        Assert.Equal("test-api-key", handler.AuthorizationParameter);
        Assert.Contains("inventory_promotion_proposal", handler.RequestBody);
        Assert.Contains(productId.ToString(), handler.RequestBody);
        Assert.Contains("\"store\":false", handler.RequestBody);
    }

    [Fact]
    public async Task GenerateProposalAsync_RejectsMissingStructuredOutput()
    {
        var model = new OpenAiPromotionProposalModel(
            new HttpClient(new RecordingHandler("{\"output\":[]}")),
            Options.Create(OpenAiOptions()));

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            model.GenerateProposalAsync(Context(Guid.NewGuid()), default));
    }

    [Fact]
    public void Options_RequireAKeyForOpenAiProvider()
    {
        var options = OpenAiOptions();
        options.ApiKey = null;

        Assert.Throws<InvalidOperationException>(options.ValidateModelProvider);
    }

    private static InventoryPromotionAgentOptions OpenAiOptions() => new()
    {
        Provider = "OpenAI",
        Model = "gpt-5-mini",
        Endpoint = "https://api.openai.com/v1/responses",
        ApiKey = "test-api-key"
    };

    private static PromotionAgentContext Context(Guid productId) => new(
        "Find a justified promotion for declining products.",
        PromotionAgentFocus.DecliningSales,
        30,
        3,
        20,
        new DateTime(2026, 10, 15, 12, 0, 0, DateTimeKind.Utc),
        Array.Empty<Guid>(),
        null,
        Node(new { items = new[] { new { productId, productName = "Linen Shirt", unitsSold = 2, previousUnitsSold = 5 } } }),
        Node(new { items = new[] { new { productId, isActive = true, availableQuantity = 20, reorderLevel = 5 } } }),
        Node(new { items = Array.Empty<object>() }),
        Node(new { products = new[] { new { productId, productName = "Linen Shirt", hasActivePromotion = false } } }),
        Node(new { products = new[] { new { productId, variants = new[] { new { price = 2500m } } } } }));

    private static JsonNode Node(object value) =>
        JsonSerializer.SerializeToNode(value)!;

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly string _responseBody;

        public RecordingHandler(string responseBody)
        {
            _responseBody = responseBody;
        }

        public string? AuthorizationScheme { get; private set; }

        public string? AuthorizationParameter { get; private set; }

        public string RequestBody { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            AuthorizationScheme = request.Headers.Authorization?.Scheme;
            AuthorizationParameter = request.Headers.Authorization?.Parameter;
            RequestBody = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    _responseBody,
                    Encoding.UTF8,
                    "application/json")
            };
        }
    }
}
