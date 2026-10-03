using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using SEF_Project.Api.DTOs.Agents;

namespace SEF_Project.Api.AI.FulfilmentException;

public sealed record ValidationOutcome(string Rule, bool IsValid, string Message);

/// <summary>
/// Deterministic checks on model output. Nothing is repaired: any failure
/// rejects the whole resolution (fail closed).
/// </summary>
public static class FulfilmentExceptionValidator
{
    private static readonly JsonSerializerOptions StrictOptions =
        new(JsonSerializerDefaults.Web)
        {
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
        };

    /// <summary>Strict schema parsing of the raw model output.</summary>
    public static FulfilmentExceptionDocument Parse(string? raw, int maxCharacters)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            throw new ResolutionSchemaException("The model returned no output.");
        }

        if (raw.Length > maxCharacters)
        {
            throw new ResolutionSchemaException("The model output is too long.");
        }

        FulfilmentExceptionDocument? document;
        try
        {
            document = JsonSerializer.Deserialize<FulfilmentExceptionDocument>(raw, StrictOptions);
        }
        catch (JsonException)
        {
            throw new ResolutionSchemaException(
                "The model output is not a valid resolution JSON document.");
        }

        if (document is null)
        {
            throw new ResolutionSchemaException("The model output is empty.");
        }

        if (document.SchemaVersion != FulfilmentExceptionAgentConstants.SchemaVersion)
        {
            throw new ResolutionSchemaException(
                $"Unsupported schemaVersion; expected '{FulfilmentExceptionAgentConstants.SchemaVersion}'.");
        }

        if (string.IsNullOrWhiteSpace(document.Summary) || document.Summary.Length > 1000)
        {
            throw new ResolutionSchemaException(
                "'summary' is required and must be at most 1000 characters.");
        }

        if (document.Resolution is null)
        {
            throw new ResolutionSchemaException("'resolution' is required.");
        }

        if (string.IsNullOrWhiteSpace(document.Resolution.TargetType))
        {
            throw new ResolutionSchemaException("'resolution.targetType' is required.");
        }

        if (string.IsNullOrWhiteSpace(document.Resolution.Action))
        {
            throw new ResolutionSchemaException("'resolution.action' is required.");
        }

        if (string.IsNullOrWhiteSpace(document.Resolution.Rationale)
            || document.Resolution.Rationale.Length is < 10 or > 500)
        {
            throw new ResolutionSchemaException(
                "'resolution.rationale' is required and must be 10-500 characters.");
        }

        return document;
    }

    public static List<ValidationOutcome> Validate(
        FulfilmentExceptionDocument document,
        JsonNode permittedTransitions)
    {
        var results = new List<ValidationOutcome>();
        var resolution = document.Resolution;
        var transitions = permittedTransitions["transitions"];

        var recognized = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Order",
            "Payment",
            "Shipment",
            "None"
        };
        var targetTypeValid = recognized.Contains(resolution.TargetType);
        results.Add(Outcome(
            "TargetType",
            targetTypeValid,
            targetTypeValid
                ? $"Target type '{resolution.TargetType}' is recognised."
                : $"Target type '{resolution.TargetType}' is not recognised."));

        if (string.Equals(resolution.TargetType, "None", StringComparison.OrdinalIgnoreCase))
        {
            var noAction = string.Equals(
                    resolution.Action,
                    "NoAction",
                    StringComparison.OrdinalIgnoreCase)
                && resolution.TargetId is null;
            results.Add(Outcome(
                "NoAction",
                noAction,
                noAction
                    ? "No permitted transition; no action proposed."
                    : "A 'None' resolution must have action 'NoAction' and no target id."));
            return results;
        }

        var target = FindTarget(transitions, resolution.TargetType, resolution.TargetId);
        results.Add(Outcome(
            "TargetExists",
            target is not null,
            target is not null
                ? $"The {resolution.TargetType.ToLowerInvariant()} '{resolution.TargetId}' exists."
                : $"The {resolution.TargetType.ToLowerInvariant()} '{resolution.TargetId}' does not exist."));

        if (target is null)
        {
            return results;
        }

        var currentStatus = ReadString(target, "currentStatus");
        var evidenceStatus = resolution.Evidence?.CurrentStatus;
        var statusMatches = string.Equals(
            evidenceStatus,
            currentStatus,
            StringComparison.OrdinalIgnoreCase);
        results.Add(Outcome(
            "StatusMatches",
            statusMatches,
            statusMatches
                ? $"Reported status '{evidenceStatus}' matches the current '{currentStatus}' status."
                : $"Reported status '{evidenceStatus}' does not match the current '{currentStatus}' status."));

        var allowed = ReadAllowed(target);
        var transitionAllowed = allowed.Contains(
            resolution.Action,
            StringComparer.OrdinalIgnoreCase);
        results.Add(Outcome(
            "TransitionAllowed",
            transitionAllowed,
            transitionAllowed
                ? $"Action '{resolution.Action}' is a permitted transition from '{currentStatus}'."
                : $"Action '{resolution.Action}' is not a permitted transition from '{currentStatus}'."));

        return results;
    }

    private static ValidationOutcome Outcome(string rule, bool isValid, string message) =>
        new(rule, isValid, message);

    private static JsonNode? FindTarget(JsonNode? transitions, string targetType, Guid? targetId)
    {
        if (transitions is null || targetId is null)
        {
            return null;
        }

        return targetType.ToLowerInvariant() switch
        {
            "order" => transitions["order"],
            "payment" => FindById(transitions["payments"] as JsonArray, "paymentId", targetId.Value),
            "shipment" => FindById(transitions["shipments"] as JsonArray, "shipmentId", targetId.Value),
            _ => null
        };
    }

    private static JsonNode? FindById(JsonArray? array, string idProperty, Guid id)
    {
        if (array is null)
        {
            return null;
        }

        foreach (var item in array)
        {
            if (item is JsonObject obj
                && obj[idProperty] is JsonValue value
                && value.TryGetValue<string>(out var text)
                && Guid.TryParse(text, out var guid)
                && guid == id)
            {
                return item;
            }
        }

        return null;
    }

    private static string? ReadString(JsonNode? node, string property) =>
        node?[property] is JsonValue value && value.TryGetValue<string>(out var text)
            ? text
            : null;

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
