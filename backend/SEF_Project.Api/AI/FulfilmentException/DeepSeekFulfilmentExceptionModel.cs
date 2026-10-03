using System.Text.Json;
using SEF_Project.Api.AI.DeepSeek;

namespace SEF_Project.Api.AI.FulfilmentException;

/// <summary>
/// Model-backed resolution boundary for the Fulfilment Exception Agent. The
/// model receives only the structured objective and the results of allow-listed
/// read-only tools; its output is parsed strictly and validated deterministically
/// before use.
/// </summary>
public sealed class DeepSeekFulfilmentExceptionModel : IFulfilmentExceptionModel
{
    private const string SystemInstruction = """
        You are Clothic's Fulfilment Exception Agent for a fashion commerce platform.
        Investigate the supplied order, payment, shipment, status-history and permitted-transition data,
        then return one JSON object with this exact shape:
        {
          "schemaVersion": "1.0",
          "summary": "string",
          "resolution": {
            "targetType": "Order | Payment | Shipment | None",
            "targetId": "string GUID or null",
            "action": "string",
            "rationale": "string",
            "evidence": { "currentStatus": "string or null" }
          }
        }
        Propose only an action that appears in the supplied permitted transitions for the chosen target.
        Use only identifiers and statuses present in the supplied data; never invent an order, payment, shipment, status or transition.
        targetType must be exactly one of Order, Payment, Shipment, or None.
        When targetType is None, targetId must be null and action must be NoAction.
        Return JSON only, with no markdown or explanatory text outside the JSON object.
        """;

    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    private readonly IDeepSeekChatCompletionsClient _client;

    public DeepSeekFulfilmentExceptionModel(IDeepSeekChatCompletionsClient client)
    {
        _client = client;
    }

    public string Name => "DeepSeek";

    public Task<string> GenerateResolutionAsync(
        FulfilmentExceptionContext context,
        CancellationToken cancellationToken = default)
    {
        var inputJson = JsonSerializer.Serialize(new
        {
            context.Objective,
            context.OrderId,
            context.ReviewerFeedback,
            order = context.Order,
            payments = context.Payments,
            shipments = context.Shipments,
            history = context.History,
            permittedTransitions = context.PermittedTransitions
        }, JsonOptions);

        return _client.CompleteJsonAsync(SystemInstruction, inputJson, cancellationToken);
    }
}
