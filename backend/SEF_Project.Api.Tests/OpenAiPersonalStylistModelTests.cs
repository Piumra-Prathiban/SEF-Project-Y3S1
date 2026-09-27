using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using SEF_Project.Api.Services.Recommendations;

namespace SEF_Project.Api.Tests;

public class OpenAiPersonalStylistModelTests
{
    [Fact]
    public async Task GenerateAsync_SendsGroundedContextAndParsesStructuredOutput()
    {
        var productId = Guid.NewGuid();
        var variantId = Guid.NewGuid();
        var modelOutput = JsonSerializer.Serialize(new
        {
            recommendations = new[]
            {
                new
                {
                    productId,
                    variantId,
                    price = 3500m,
                    quantity = 1,
                    size = "M",
                    colour = "Navy",
                    reason = "Matches the requested smart-casual occasion."
                }
            }
        });
        var responseBody = JsonSerializer.Serialize(new
        {
            output = new[]
            {
                new
                {
                    content = new[]
                    {
                        new { type = "output_text", text = modelOutput }
                    }
                }
            }
        });
        var handler = new RecordingHandler(responseBody);
        var model = new OpenAiPersonalStylistModel(
            new HttpClient(handler),
            Options.Create(OpenAiOptions()));

        var result = await model.GenerateAsync(BuildInput(productId, variantId));

        var recommendation = Assert.Single(result.Recommendations);
        Assert.Equal(productId, recommendation.ProductId);
        Assert.Equal(variantId, recommendation.VariantId);
        Assert.Equal(3500m, recommendation.Price);
        Assert.Equal("Bearer", handler.AuthorizationScheme);
        Assert.Equal("test-api-key", handler.AuthorizationParameter);
        Assert.Contains("personal_stylist_recommendations", handler.RequestBody);
        Assert.Contains(variantId.ToString(), handler.RequestBody);
        Assert.DoesNotContain("Asha", handler.RequestBody);
    }

    [Fact]
    public async Task GenerateAsync_RejectsMissingStructuredOutput()
    {
        var model = new OpenAiPersonalStylistModel(
            new HttpClient(new RecordingHandler("{\"output\":[]}")),
            Options.Create(OpenAiOptions()));

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            model.GenerateAsync(BuildInput(Guid.NewGuid(), Guid.NewGuid())));
    }

    [Fact]
    public void Options_RequireAKeyForOpenAiProvider()
    {
        var options = OpenAiOptions();
        options.ApiKey = null;

        Assert.Throws<InvalidOperationException>(options.Validate);
    }

    private static PersonalStylistAgentOptions OpenAiOptions() => new()
    {
        Provider = "OpenAI",
        Model = "gpt-5-mini",
        Endpoint = "https://api.openai.com/v1/responses",
        ApiKey = "test-api-key"
    };

    private static PersonalStylistModelInput BuildInput(Guid productId, Guid variantId)
    {
        var variant = new RecommendationCatalogVariant(
            variantId,
            "SHIRT-M-NAVY",
            "M / Navy",
            3500m,
            4,
            "M",
            "Navy");

        return new PersonalStylistModelInput(
            new RecommendationCustomerContext(7, "Asha", "Perera"),
            new RecommendationContext(
                "Work dinner",
                5000m,
                new[] { "Navy" },
                "M",
                "Smart casual"),
            Array.Empty<WishlistToolItem>(),
            new[]
            {
                new RecommendationCatalogProduct(
                    productId,
                    "Cotton Shirt",
                    "A tailored cotton shirt.",
                    3500m,
                    Array.Empty<RecommendationCatalogCategory>(),
                    new[] { variant })
            },
            new[]
            {
                new VerifiedProductAvailability(
                    productId,
                    variantId,
                    "Cotton Shirt",
                    "M / Navy",
                    "SHIRT-M-NAVY",
                    3500m,
                    4,
                    "M",
                    "Navy")
            },
            Array.Empty<string>());
    }

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
