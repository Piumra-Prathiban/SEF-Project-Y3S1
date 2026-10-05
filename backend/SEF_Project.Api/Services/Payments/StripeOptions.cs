namespace SEF_Project.Api.Services.Payments;

/// <summary>
/// Configuration for the Stripe payment sandbox. Secrets live in the gitignored
/// <c>.env</c> / user-secrets, never in source control.
/// </summary>
public class StripeOptions
{
    public const string SectionName = "Stripe";

    public string SecretKey { get; set; } = string.Empty;

    public string PublishableKey { get; set; } = string.Empty;

    public string WebhookSecret { get; set; } = string.Empty;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(SecretKey);
}
