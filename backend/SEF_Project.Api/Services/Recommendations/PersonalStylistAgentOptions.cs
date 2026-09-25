namespace SEF_Project.Api.Services.Recommendations;

public class PersonalStylistAgentOptions
{
    public int OverallTimeoutSeconds { get; set; } = 20;

    public int ToolTimeoutSeconds { get; set; } = 5;

    public int MaximumToolAttempts { get; set; } = 2;

    public void Validate()
    {
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
