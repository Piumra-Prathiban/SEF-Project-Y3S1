using System.Text.Json.Nodes;
using SEF_Project.Api.DTOs.Agents;

namespace SEF_Project.Api.AI.FulfilmentException;

/// <summary>
/// Deterministic, data-grounded classification of why an order appears to be
/// an exception. Produced by the server (never by the model) so the diagnosis
/// is always consistent with the gathered facts.
/// </summary>
public static class FulfilmentExceptionDiagnoser
{
    public static FulfilmentExceptionDiagnosis Diagnose(
        JsonNode order,
        JsonNode payments,
        JsonNode shipments,
        JsonNode history)
    {
        var orderNode = order?["order"];
        var status = ReadString(orderNode, "status");
        var address = orderNode?["deliveryAddress"];
        var paymentStatuses = ReadStatuses(payments?["payments"], "status");
        var shipmentStatuses = ReadStatuses(shipments?["shipments"], "status");

        if (paymentStatuses.Contains("Failed", StringComparer.OrdinalIgnoreCase))
        {
            return Diagnosis(
                FulfilmentExceptionDiagnosisTypes.PaymentFailed,
                FulfilmentExceptionSeverities.Critical,
                "At least one payment is marked Failed.");
        }

        if (address is null
            || IsMissing(address, "line1")
            || IsMissing(address, "city")
            || IsMissing(address, "postalCode"))
        {
            return Diagnosis(
                FulfilmentExceptionDiagnosisTypes.AddressIssue,
                FulfilmentExceptionSeverities.High,
                "The delivery address is missing or incomplete.");
        }

        if (paymentStatuses.Contains("Pending", StringComparer.OrdinalIgnoreCase)
            && !string.Equals(status, "Pending", StringComparison.OrdinalIgnoreCase))
        {
            return Diagnosis(
                FulfilmentExceptionDiagnosisTypes.PaymentPending,
                FulfilmentExceptionSeverities.High,
                "A payment is still pending even though the order has advanced.");
        }

        if ((shipmentStatuses.Contains("Pending", StringComparer.OrdinalIgnoreCase)
                || shipmentStatuses.Contains("Shipped", StringComparer.OrdinalIgnoreCase))
            && (string.Equals(status, "Ready", StringComparison.OrdinalIgnoreCase)
                || string.Equals(status, "Completed", StringComparison.OrdinalIgnoreCase)))
        {
            return Diagnosis(
                FulfilmentExceptionDiagnosisTypes.ShipmentStalled,
                FulfilmentExceptionSeverities.Medium,
                "A shipment has not advanced while the order is ready or completed.");
        }

        if (string.Equals(status, "Preparing", StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, "Ready", StringComparison.OrdinalIgnoreCase))
        {
            return Diagnosis(
                FulfilmentExceptionDiagnosisTypes.OrderStuck,
                FulfilmentExceptionSeverities.Medium,
                $"The order is stuck in '{status}' with no payment or shipment cause.");
        }

        return Diagnosis(
            FulfilmentExceptionDiagnosisTypes.NoException,
            FulfilmentExceptionSeverities.Low,
            "No payment, shipment, address or order-status exception was detected.");
    }

    private static FulfilmentExceptionDiagnosis Diagnosis(string type, string severity, string reason) =>
        new() { Type = type, Severity = severity, Reason = reason };

    private static string? ReadString(JsonNode? node, string property) =>
        node?[property] is JsonValue value && value.TryGetValue<string>(out var text)
            ? text
            : null;

    private static bool IsMissing(JsonNode? node, string property) =>
        string.IsNullOrWhiteSpace(ReadString(node, property));

    private static List<string> ReadStatuses(JsonNode? array, string property)
    {
        if (array is not JsonArray items)
        {
            return new List<string>();
        }

        return items
            .Select(item => ReadString(item, property))
            .Where(text => text is not null)
            .Cast<string>()
            .ToList();
    }
}
