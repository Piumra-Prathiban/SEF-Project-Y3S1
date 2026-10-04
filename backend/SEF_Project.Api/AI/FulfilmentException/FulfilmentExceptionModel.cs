using System.Text.Json;
using System.Text.Json.Nodes;
using SEF_Project.Api.DTOs.Agents;

namespace SEF_Project.Api.AI.FulfilmentException;

/// <summary>
/// Everything the resolution model may see: the objective, reviewer feedback,
/// and the results of the allow-listed read-only tools. No database, services
/// or secrets.
/// </summary>
public sealed record FulfilmentExceptionContext(
    string Objective,
    Guid OrderId,
    string? ReviewerFeedback,
    JsonNode Order,
    JsonNode Payments,
    JsonNode Shipments,
    JsonNode History,
    JsonNode PermittedTransitions);

/// <summary>
/// Replaceable model boundary. Implementations return raw text that must be a
/// <see cref="FulfilmentExceptionDocument"/> JSON document; it is parsed
/// strictly and validated deterministically before use.
/// </summary>
public interface IFulfilmentExceptionModel
{
    string Name { get; }

    Task<string> GenerateResolutionAsync(
        FulfilmentExceptionContext context,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Default model: a deterministic, data-grounded policy that proposes the next
/// permitted state-machine transition (order first, then a stuck payment or
/// shipment), or no action when nothing is permitted.
/// </summary>
public sealed class LocalFulfilmentExceptionModel : IFulfilmentExceptionModel
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    public string Name => "LocalFulfilmentPolicy";

    public Task<string> GenerateResolutionAsync(
        FulfilmentExceptionContext context,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var document = BuildResolution(context);
        return Task.FromResult(JsonSerializer.Serialize(document, JsonOptions));
    }

    private static FulfilmentExceptionDocument BuildResolution(
        FulfilmentExceptionContext context)
    {
        var transitions = context.PermittedTransitions["transitions"];
        var order = transitions?["order"];
        var orderStatus = ReadString(order, "currentStatus");
        var orderAllowed = ReadAllowed(order);

        if (orderAllowed.Count > 0)
        {
            return CreateDocument(
                "Order",
                ReadGuid(order, "orderId"),
                orderAllowed[0],
                orderStatus,
                $"The order is '{orderStatus}' and can advance to '{orderAllowed[0]}'.");
        }

        foreach (var payment in AsArray(transitions, "payments"))
        {
            var allowed = ReadAllowed(payment);
            if (allowed.Count == 0)
            {
                continue;
            }

            var status = ReadString(payment, "currentStatus");
            return CreateDocument(
                "Payment",
                ReadGuid(payment, "paymentId"),
                allowed[0],
                status,
                $"A payment is stuck in '{status}' and can advance to '{allowed[0]}'.");
        }

        foreach (var shipment in AsArray(transitions, "shipments"))
        {
            var allowed = ReadAllowed(shipment);
            if (allowed.Count == 0)
            {
                continue;
            }

            var status = ReadString(shipment, "currentStatus");
            return CreateDocument(
                "Shipment",
                ReadGuid(shipment, "shipmentId"),
                allowed[0],
                status,
                $"A shipment is stuck in '{status}' and can advance to '{allowed[0]}'.");
        }

        return CreateDocument(
            "None",
            null,
            "NoAction",
            orderStatus,
            "The order, payments and shipments have no permitted transition; nothing is stalled.");
    }

    private static FulfilmentExceptionDocument CreateDocument(
        string targetType,
        Guid? targetId,
        string action,
        string? currentStatus,
        string rationale) =>
        new()
        {
            SchemaVersion = FulfilmentExceptionAgentConstants.SchemaVersion,
            Summary = rationale,
            Resolution = new FulfilmentResolution
            {
                TargetType = targetType,
                TargetId = targetId,
                Action = action,
                Rationale = rationale,
                Evidence = new FulfilmentResolutionEvidence
                {
                    CurrentStatus = currentStatus
                }
            }
        };

    private static JsonArray AsArray(JsonNode? node, string property) =>
        node?[property] as JsonArray ?? new JsonArray();

    private static string? ReadString(JsonNode? node, string property) =>
        node?[property] is JsonValue value && value.TryGetValue<string>(out var text)
            ? text
            : null;

    private static Guid? ReadGuid(JsonNode? node, string property)
    {
        var text = ReadString(node, property);
        return text is not null && Guid.TryParse(text, out var guid) && guid != Guid.Empty
            ? guid
            : null;
    }

    private static List<string> ReadAllowed(JsonNode? node)
    {
        if (node?["allowed"] is not JsonArray array)
        {
            return new List<string>();
        }

        return array
            .Select(item =>
                item is JsonValue value && value.TryGetValue<string>(out var text)
                    ? text
                    : null)
            .OfType<string>()
            .ToList();
    }
}
