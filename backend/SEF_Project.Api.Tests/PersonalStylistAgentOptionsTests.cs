using SEF_Project.Api.Services.Recommendations;

namespace SEF_Project.Api.Tests;

public class PersonalStylistAgentOptionsTests
{
    [Theory]
    [InlineData("Local")]
    [InlineData("local")]
    [InlineData("DeepSeek")]
    [InlineData("deepseek")]
    public void Validate_WithSupportedProvider_DoesNotThrow(string provider)
    {
        var options = new PersonalStylistAgentOptions
        {
            Provider = provider
        };

        options.Validate();
    }

    [Fact]
    public void Validate_WithUnknownProvider_ThrowsClearError()
    {
        var options = new PersonalStylistAgentOptions
        {
            Provider = "Unknown"
        };

        var exception = Assert.Throws<InvalidOperationException>(options.Validate);

        Assert.Equal(
            "Personal Stylist provider must be Local or DeepSeek.",
            exception.Message);
        Assert.DoesNotContain("OpenAI", exception.Message);
    }

    [Fact]
    public void Defaults_ToLocalAndRetainsAgentExecutionSettingsOnly()
    {
        var options = new PersonalStylistAgentOptions();
        var propertyNames = typeof(PersonalStylistAgentOptions)
            .GetProperties()
            .Select(property => property.Name)
            .ToArray();

        Assert.Equal("Local", options.Provider);
        Assert.Contains(nameof(options.OverallTimeoutSeconds), propertyNames);
        Assert.Contains(nameof(options.ToolTimeoutSeconds), propertyNames);
        Assert.Contains(nameof(options.MaximumToolAttempts), propertyNames);
        Assert.DoesNotContain("Model", propertyNames);
        Assert.DoesNotContain("Endpoint", propertyNames);
        Assert.DoesNotContain("ApiKey", propertyNames);
    }
}
