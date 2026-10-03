using SEF_Project.Api.Services.AgenticAI;

namespace SEF_Project.Api.Tests;

public class InventoryAnalysisAgentOptionsTests
{
    [Theory]
    [InlineData("Local")]
    [InlineData("local")]
    [InlineData("DeepSeek")]
    [InlineData("deepseek")]
    public void Validate_WithSupportedProvider_DoesNotThrow(string provider)
    {
        var options = new InventoryAnalysisAgentOptions
        {
            Provider = provider
        };

        options.Validate();
    }

    [Fact]
    public void Validate_WithUnknownProvider_ThrowsClearError()
    {
        var options = new InventoryAnalysisAgentOptions
        {
            Provider = "Unknown"
        };

        var exception = Assert.Throws<InvalidOperationException>(
            options.Validate);

        Assert.Equal(
            "Inventory Analysis Agent provider must be Local or DeepSeek.",
            exception.Message);
    }
}
