using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SEF_Project.Api.Services.Orders;
using SEF_Project.Api.Services.Payments;
using Stripe;

namespace SEF_Project.Api.Controllers;

/// <summary>
/// Receives Stripe webhook events and applies their outcome to the order's
/// payment through the normal order service (never a direct write). The endpoint
/// is unauthenticated but every request is verified against the configured
/// webhook signing secret.
/// </summary>
[ApiController]
[Route("api/webhooks/stripe")]
[AllowAnonymous]
public class StripeWebhookController : ControllerBase
{
    private readonly IStripePaymentService _stripePaymentService;
    private readonly IOrderService _orderService;

    public StripeWebhookController(
        IStripePaymentService stripePaymentService,
        IOrderService orderService)
    {
        _stripePaymentService = stripePaymentService;
        _orderService = orderService;
    }

    [HttpPost]
    public async Task<IActionResult> HandleWebhook(CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(Request.Body);
        var json = await reader.ReadToEndAsync(cancellationToken);
        var signature = Request.Headers["Stripe-Signature"].ToString();

        if (string.IsNullOrWhiteSpace(signature))
        {
            return BadRequest();
        }

        StripePaymentEvent? paymentEvent;
        try
        {
            paymentEvent = _stripePaymentService.ParseWebhookEvent(json, signature);
        }
        catch (StripeException)
        {
            return BadRequest("Invalid webhook signature.");
        }
        catch (InvalidOperationException)
        {
            return BadRequest();
        }

        // Unhandled event types (e.g. checkout.session.completed) are acknowledged
        // so Stripe does not keep retrying, but they have no side effect here.
        if (paymentEvent is null)
        {
            return Ok();
        }

        var applied = await _orderService.ApplyPaymentIntentResultAsync(
            paymentEvent.PaymentIntentId,
            paymentEvent.Succeeded,
            cancellationToken);

        return applied ? Ok() : NotFound();
    }
}
