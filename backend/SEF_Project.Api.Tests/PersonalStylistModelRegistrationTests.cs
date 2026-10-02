using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using SEF_Project.Api.Services.Recommendations;

namespace SEF_Project.Api.Tests;

public class PersonalStylistModelRegistrationTests :
    IClassFixture<MarketingApiFactory>
{
    private readonly MarketingApiFactory _factory;

    public PersonalStylistModelRegistrationTests(
        MarketingApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public void DefaultProvider_ResolvesGroundedModel()
    {
        using var scope = _factory.Services.CreateScope();

        Assert.IsType<GroundedPersonalStylistModel>(
            scope.ServiceProvider.GetRequiredService<
                IPersonalStylistRecommendationModel>());
    }

    [Fact]
    public void DeepSeekProvider_ResolvesDeepSeekModelCaseInsensitively()
    {
        using var deepSeekFactory = _factory.WithWebHostBuilder(builder =>
            builder.UseSetting(
                "PersonalStylistAgent:Provider",
                "deepseek"));
        using var scope = deepSeekFactory.Services.CreateScope();

        Assert.IsType<DeepSeekPersonalStylistModel>(
            scope.ServiceProvider.GetRequiredService<
                IPersonalStylistRecommendationModel>());
    }
}
