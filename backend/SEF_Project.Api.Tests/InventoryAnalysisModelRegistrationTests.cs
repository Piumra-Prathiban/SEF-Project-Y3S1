using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using SEF_Project.Api.Services.AgenticAI;

namespace SEF_Project.Api.Tests;

public class InventoryAnalysisModelRegistrationTests :
    IClassFixture<MarketingApiFactory>
{
    private readonly MarketingApiFactory _factory;

    public InventoryAnalysisModelRegistrationTests(
        MarketingApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public void LocalProvider_ResolvesLocalPolicy()
    {
        using var localFactory = _factory.WithWebHostBuilder(builder =>
            builder.UseSetting(
                "InventoryAnalysisAgent:Provider",
                "Local"));
        using var scope = localFactory.Services.CreateScope();

        Assert.IsType<LocalInventoryAnalysisModelClient>(
            scope.ServiceProvider.GetRequiredService<
                IInventoryAnalysisModelClient>());
    }

    [Fact]
    public void DeepSeekProvider_ResolvesDeepSeekClientCaseInsensitively()
    {
        using var deepSeekFactory = _factory.WithWebHostBuilder(builder =>
            builder.UseSetting(
                "InventoryAnalysisAgent:Provider",
                "deepseek"));
        using var scope = deepSeekFactory.Services.CreateScope();

        Assert.IsType<DeepSeekInventoryAnalysisModelClient>(
            scope.ServiceProvider.GetRequiredService<
                IInventoryAnalysisModelClient>());
    }
}
