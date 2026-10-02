using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SEF_Project.Api.AI.DeepSeek;
using SEF_Project.Api.Services.Recommendations;

namespace SEF_Project.Api.Tests;

public class DeepSeekPersonalStylistModelTests
{
    [Fact]
    public async Task GenerateAsync_SendsGroundedJsonAndReturnsTypedOutput()
    {
        var productId = Guid.NewGuid();
        var variantId = Guid.NewGuid();
        var client = new RecordingDeepSeekClient(JsonSerializer.Serialize(new
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
                    reason = "Matches the requested smart-casual style."
                }
            }
        }));
        var model = new DeepSeekPersonalStylistModel(client);
        var input = BuildInput(productId, variantId);
        using var cancellation = new CancellationTokenSource();

        var result = await model.GenerateAsync(input, cancellation.Token);

        var recommendation = Assert.Single(result.Recommendations);
        Assert.Equal(productId, recommendation.ProductId);
        Assert.Equal(variantId, recommendation.VariantId);
        Assert.Equal(3500m, recommendation.Price);
        Assert.Contains("Clothic Personal Stylist", client.SystemInstruction);
        Assert.Contains("JSON", client.SystemInstruction);
        Assert.Contains("at most five", client.SystemInstruction);
        Assert.Contains("available quantity", client.SystemInstruction);
        Assert.Equal(cancellation.Token, client.CancellationToken);

        using var inputJson = JsonDocument.Parse(client.UserJsonInput);
        var root = inputJson.RootElement;
        Assert.Equal(7, root.GetProperty("customer").GetProperty("customerId").GetInt32());
        Assert.Equal("Work dinner", root.GetProperty("preferences").GetProperty("occasion").GetString());
        Assert.Equal(productId, root.GetProperty("wishlistItems")[0].GetProperty("productId").GetGuid());
        Assert.Equal(productId, root.GetProperty("catalogueProducts")[0].GetProperty("productId").GetGuid());
        Assert.Equal(variantId, root.GetProperty("availableVariants")[0].GetProperty("variantId").GetGuid());
        Assert.Equal("occasion", root.GetProperty("relaxedCriteria")[0].GetString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-json")]
    public async Task GenerateAsync_RejectsEmptyOrUnreadableOutput(string output)
    {
        var model = new DeepSeekPersonalStylistModel(
            new RecordingDeepSeekClient(output));

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            model.GenerateAsync(BuildInput(Guid.NewGuid(), Guid.NewGuid())));
    }

    [Fact]
    public void Registration_ResolvesThroughDependencyInjection()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IDeepSeekChatCompletionsClient>(
            new RecordingDeepSeekClient("{\"recommendations\":[]}"));
        services.AddScoped<DeepSeekPersonalStylistModel>();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        Assert.IsType<DeepSeekPersonalStylistModel>(
            scope.ServiceProvider.GetRequiredService<
                DeepSeekPersonalStylistModel>());
    }

    private static PersonalStylistModelInput BuildInput(
        Guid productId,
        Guid variantId)
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
            new[] { new WishlistToolItem(productId, "Cotton Shirt", true) },
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
            new[] { "occasion" });
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
