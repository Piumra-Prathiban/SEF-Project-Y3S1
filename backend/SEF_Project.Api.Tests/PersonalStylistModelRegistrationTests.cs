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
    public void LocalProvider_ResolvesGroundedModel()
    {
        using var localFactory = _factory.WithWebHostBuilder(builder =>
            builder.UseSetting(
                "PersonalStylistAgent:Provider",
                "Local"));
        using var scope = localFactory.Services.CreateScope();

        Assert.IsType<GroundedPersonalStylistModel>(
            scope.ServiceProvider.GetRequiredService<
                IPersonalStylistRecommendationModel>());
    }

    [Fact]
    public void DeepSeekProvider_ResolvesDeepSeekModel()
    {
        using var deepSeekFactory = _factory.WithWebHostBuilder(builder =>
            builder.UseSetting(
                "PersonalStylistAgent:Provider",
                "DeepSeek"));
        using var scope = deepSeekFactory.Services.CreateScope();

        Assert.IsType<DeepSeekPersonalStylistModel>(
            scope.ServiceProvider.GetRequiredService<
                IPersonalStylistRecommendationModel>());
    }
}
