using SEF_Project.Api.Configuration;

namespace SEF_Project.Api.Tests;

public class SecurityConfigurationTests
{
    [Fact]
    public void CorsOrigins_RequireExplicitHttpOrHttpsOrigins()
    {
        var origins = CorsOriginPolicy.Normalize(new[]
        {
            " http://localhost:5173/ ",
            "https://localhost:5173",
            "HTTP://LOCALHOST:5173"
        });

        Assert.Equal(2, origins.Length);
        Assert.Contains("http://localhost:5173", origins);
        Assert.Contains("https://localhost:5173", origins);
    }

    [Theory]
    [InlineData("*")]
    [InlineData("file:///tmp/catalogue")]
    [InlineData("https://example.com/path")]
    [InlineData("https://user@example.com")]
    public void CorsOrigins_RejectWildcardAndNonOriginValues(string origin)
    {
        Assert.Throws<InvalidOperationException>(() =>
            CorsOriginPolicy.Normalize(new[] { origin }));
    }

    [Fact]
    public void CorsOrigins_RejectMissingConfiguration()
    {
        Assert.Throws<InvalidOperationException>(() =>
            CorsOriginPolicy.Normalize(Array.Empty<string>()));
    }
}
