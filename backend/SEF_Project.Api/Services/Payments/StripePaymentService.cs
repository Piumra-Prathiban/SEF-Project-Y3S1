using Microsoft.Extensions.Options;
using Stripe;

namespace SEF_Project.Api.Services.Payments;

public interface IStripePaymentService
{
    /// <summary>
    /// Creates a Stripe PaymentIntent for an order total. Returns null (and logs)
    /// when Stripe is not configured, so checkout still works without a key and
    /// the payment stays pending for manual confirmation.
    /// </summary>
    Task<StripePaymentIntentResult?> CreatePaymentIntentAsync(
        decimal amount,
        string currency,
        string orderNumber,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifies a webhook payload against the configured signing secret and
    /// normalizes it. Returns null for events we do not handle. Throws
    /// <see cref="StripeException"/> when the signature is invalid.
    /// </summary>
    StripePaymentEvent? ParseWebhookEvent(string json, string signatureHeader);
}

public sealed record StripePaymentIntentResult(string Id, string ClientSecret);

public sealed record StripePaymentEvent(string PaymentIntentId, bool Succeeded);

public class StripePaymentService : IStripePaymentService
{
    private readonly StripeOptions _options;
    private readonly ILogger<StripePaymentService> _logger;

    public StripePaymentService(
        IOptions<StripeOptions> options,
        ILogger<StripePaymentService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<StripePaymentIntentResult?> CreatePaymentIntentAsync(
        decimal amount,
        string currency,
        string orderNumber,
        CancellationToken cancellationToken = default)
    {
        if (!_options.IsConfigured)
        {
            _logger.LogWarning(
                "Stripe is not configured (missing Stripe:SecretKey); "
                + "skipping PaymentIntent creation.");
            return null;
        }

        try
        {
            StripeConfiguration.ApiKey = _options.SecretKey;

            // LKR is a two-decimal currency in Stripe, so the amount is expressed
            // in minor units (rupees × 100). The order total is a decimal in
            // rupees (e.g. 2500.00 → 250000).
            var minorAmount =
                (long)Math.Round(amount * 100m, 0, MidpointRounding.AwayFromZero);

            var service = new PaymentIntentService();
            var options = new PaymentIntentCreateOptions
            {
                Amount = minorAmount,
                Currency = currency.ToLowerInvariant(),
                AutomaticPaymentMethods = new PaymentIntentAutomaticPaymentMethodsOptions
                {
                    Enabled = true
                },
                Metadata = new Dictionary<string, string>
                {
                    ["order_number"] = orderNumber
                }
            };

            var intent = await service.CreateAsync(options, null, cancellationToken);

            return new StripePaymentIntentResult(intent.Id, intent.ClientSecret);
        }
        catch (Exception exception)
        {
            // Fail closed on the card flow: if the PaymentIntent cannot be
            // created (invalid key, amount below Stripe's minimum, network
            // failure, …) the order is still placed with a Pending payment and
            // staff can confirm it manually rather than failing checkout.
            _logger.LogWarning(
                exception,
                "Stripe PaymentIntent creation failed; falling back to the pending-payment flow.");
            return null;
        }
    }

    public StripePaymentEvent? ParseWebhookEvent(string json, string signatureHeader)
    {
        if (string.IsNullOrWhiteSpace(_options.WebhookSecret))
        {
            throw new InvalidOperationException(
                "Stripe webhook secret is not configured.");
        }

        var stripeEvent = EventUtility.ConstructEvent(
            json,
            signatureHeader,
            _options.WebhookSecret,
            tolerance: 300,
            throwOnApiVersionMismatch: false);

        if (stripeEvent.Type is not ("payment_intent.succeeded"
            or "payment_intent.payment_failed"))
        {
            return null;
        }

        var paymentIntent = stripeEvent.Data.Object as PaymentIntent;
        if (paymentIntent is null)
        {
            return null;
        }

        return new StripePaymentEvent(
            paymentIntent.Id,
            stripeEvent.Type == "payment_intent.succeeded");
    }
}
