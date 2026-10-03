namespace SEF_Project.Api.Services.AgenticAI;

public class InventoryAnalysisAgentOptions
{
    public const string SectionName = "InventoryAnalysisAgent";

    public string Provider { get; set; } = "Local";

    public void Validate()
    {
        if (!string.Equals(Provider, "Local", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(Provider, "DeepSeek", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Inventory Analysis Agent provider must be Local or DeepSeek.");
        }
    }
}
