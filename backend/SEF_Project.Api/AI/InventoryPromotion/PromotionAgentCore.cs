using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace SEF_Project.Api.AI.InventoryPromotion;

public static class PromotionAgentConstants
{
    public const string AgentName = "Inventory & Promotion Agent";
    public const string ValidatorName = "Deterministic Validator";
    public const string ReviewerName = "Human Reviewer";
    public const string ExecutorName = "Promotion Service";

    public const string SchemaVersion = "1.0";

    public const string GetSalesVelocity = "GetSalesVelocity";
    public const string GetInventory = "GetInventory";
    public const string GetActivePromotions = "GetActivePromotions";
    public const string GetProductDetails = "GetProductDetails";
    public const string GetProductPricing = "GetProductPricing";
    public const string CalculatePromotion = "CalculatePromotion";

    /// <summary>The model's structured output is recorded under this name.</summary>
    public const string SubmitProposal = "SubmitPromotionProposal";

    /// <summary>The executor's write (after approval) is recorded under this name.</summary>
    public const string ApprovedAction = "ApprovedAction:CreatePromotions";

    /// <summary>The only tools this agent may call. All are read-only.</summary>
    public static readonly IReadOnlySet<string> AllowedTools = new HashSet<string>(StringComparer.Ordinal)
    {
        GetSalesVelocity,
        GetInventory,
        GetActivePromotions,
        GetProductDetails,
        GetProductPricing,
        CalculatePromotion
    };

    public static readonly IReadOnlySet<string> AllowedPromotionTypes = new HashSet<string>(StringComparer.Ordinal)
    {
        "PercentageDiscount",
        "FixedAmountDiscount"
    };
}

public class InventoryPromotionAgentOptions
{
    public const string SectionName = "InventoryPromotionAgent";

    public TimeSpan ToolTimeout { get; set; } = TimeSpan.FromSeconds(5);

    public TimeSpan ModelTimeout { get; set; } = TimeSpan.FromSeconds(20);

    /// <summary>Retries after the first attempt, for transient tool failures only.</summary>
    public int MaxToolRetries { get; set; } = 2;

    public int MaxRevisions { get; set; } = 3;

    /// <summary>A proposal at or above this discount % is high impact.</summary>
    public int HighImpactDiscountPercent { get; set; } = 20;

    /// <summary>More proposals than this in one workflow is high impact.</summary>
    public int HighImpactProposalCount { get; set; } = 3;

    public int MaxPromotionDays { get; set; } = 60;

    public int MaxModelOutputCharacters { get; set; } = 50_000;

    public int MaxToolOutputCharacters { get; set; } = 256_000;
}

/// <summary>Invalid tool arguments; never retried.</summary>
public sealed class ToolInputException : Exception
{
    public ToolInputException(string message) : base(message) { }
}

/// <summary>A business rule rejected the tool request; never retried.</summary>
public sealed class ToolRejectedException : Exception
{
    public ToolRejectedException(string message) : base(message) { }
}

public sealed class ToolPermissionException : Exception
{
    public ToolPermissionException(string toolName)
        : base($"Tool '{toolName}' is not permitted for the {PromotionAgentConstants.AgentName}.")
    {
        ToolName = toolName;
    }

    public string ToolName { get; }
}

/// <summary>A tool failed after its bounded retries (or was rejected).</summary>
public sealed class PromotionAgentToolException : Exception
{
    public PromotionAgentToolException(string toolName, bool isTimeout, string message, bool isRejected = false)
        : base(message)
    {
        ToolName = toolName;
        IsTimeout = isTimeout;
        IsRejected = isRejected;
    }

    public string ToolName { get; }

    public bool IsTimeout { get; }

    /// <summary>Invalid input or a business-rule rejection (not a transient failure).</summary>
    public bool IsRejected { get; }
}

public sealed class ProposalSchemaException : Exception
{
    public ProposalSchemaException(string message) : base(message) { }
}

public static class AgentJson
{
    public static readonly JsonSerializerOptions Options = CreateOptions(strict: false);

    /// <summary>Rejects unknown properties; used for model output.</summary>
    public static readonly JsonSerializerOptions StrictOptions = CreateOptions(strict: true);

    private static JsonSerializerOptions CreateOptions(bool strict)
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());

        if (strict)
        {
            options.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow;
        }

        return options;
    }

    public static JsonNode ToNode(object value) =>
        JsonSerializer.SerializeToNode(value, Options)
        ?? throw new InvalidOperationException("Value serialised to null.");

    public static string Serialize(JsonNode? node) => node?.ToJsonString(Options) ?? "null";
}

/// <summary>Removes secrets from anything that is persisted in the audit trail.</summary>
public static class JsonRedactor
{
    private static readonly string[] SensitiveFragments =
        { "password", "token", "secret", "apikey", "api_key", "authorization", "connectionstring" };

    public const string Placeholder = "[REDACTED]";

    public static JsonNode? Redact(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject obj:
                var copy = new JsonObject();
                foreach (var (key, value) in obj)
                {
                    copy[key] = IsSensitive(key) ? JsonValue.Create(Placeholder) : Redact(value);
                }
                return copy;

            case JsonArray array:
                var items = new JsonArray();
                foreach (var item in array)
                {
                    items.Add(Redact(item));
                }
                return items;

            default:
                return node?.DeepClone();
        }
    }

    private static bool IsSensitive(string key)
    {
        var normalized = key.Replace("-", string.Empty).ToLowerInvariant();
        return SensitiveFragments.Any(normalized.Contains);
    }
}
