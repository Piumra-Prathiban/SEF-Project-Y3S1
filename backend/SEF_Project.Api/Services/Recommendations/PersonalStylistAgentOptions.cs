namespace SEF_Project.Api.Services.Recommendations;

public class PersonalStylistAgentOptions
{
    public string Provider { get; set; } = "Local";

    public string Model { get; set; } = "gpt-5-mini";

    public string Endpoint { get; set; } = "https://api.openai.com/v1/responses";

    public string? ApiKey { get; set; }

    public int OverallTimeoutSeconds { get; set; } = 20;

    public int ToolTimeoutSeconds { get; set; } = 5;

    public int MaximumToolAttempts { get; set; } = 2;

    public void Validate()
    {
        if (!string.Equals(Provider, "Local", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(Provider, "OpenAI", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Personal Stylist provider must be Local or OpenAI.");
        }

        if (string.Equals(Provider, "OpenAI", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(ApiKey))
            {
                throw new InvalidOperationException(
                    "Personal Stylist OpenAI provider requires an API key.");
            }

            if (string.IsNullOrWhiteSpace(Model))
            {
                throw new InvalidOperationException(
                    "Personal Stylist OpenAI provider requires a model.");
            }

            if (!Uri.TryCreate(Endpoint, UriKind.Absolute, out var endpoint) ||
                endpoint.Scheme != Uri.UriSchemeHttps)
            {
                throw new InvalidOperationException(
                    "Personal Stylist OpenAI endpoint must be an absolute HTTPS URL.");
            }
        }

        if (OverallTimeoutSeconds is < 1 or > 120)
        {
            throw new InvalidOperationException(
                "Personal Stylist overall timeout must be between 1 and 120 seconds.");
        }

        if (ToolTimeoutSeconds is < 1 or > 60)
        {
            throw new InvalidOperationException(
                "Personal Stylist tool timeout must be between 1 and 60 seconds.");
        }

        if (MaximumToolAttempts is < 1 or > 3)
        {
            throw new InvalidOperationException(
                "Personal Stylist tool attempts must be between 1 and 3.");
        }
    }
}
