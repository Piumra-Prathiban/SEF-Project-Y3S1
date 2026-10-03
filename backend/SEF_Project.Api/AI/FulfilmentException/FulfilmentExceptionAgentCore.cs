namespace SEF_Project.Api.AI.FulfilmentException;

public static class FulfilmentExceptionAgentConstants
{
    public const string AgentName = "Fulfilment Exception Agent";
    public const string ValidatorName = "Deterministic Validator";
    public const string ReviewerName = "Human Reviewer";
    public const string ExecutorName = "Order Service";

    public const string SchemaVersion = "1.0";

    public const string GetOrderDetails = "GetOrderDetails";
    public const string GetPayments = "GetPayments";
    public const string GetShipments = "GetShipments";
    public const string GetStatusHistory = "GetStatusHistory";
    public const string GetPermittedTransitions = "GetPermittedTransitions";

    /// <summary>The model's structured output is recorded under this name.</summary>
    public const string SubmitResolution = "SubmitResolution";

    /// <summary>The executor's write (after approval) is recorded under this name.</summary>
    public const string ApprovedAction = "ApprovedAction:ResolveOrder";

    /// <summary>The only tools this agent may call. All are read-only.</summary>
    public static readonly IReadOnlySet<string> AllowedTools =
        new HashSet<string>(StringComparer.Ordinal)
        {
            GetOrderDetails,
            GetPayments,
            GetShipments,
            GetStatusHistory,
            GetPermittedTransitions
        };
}

public class FulfilmentExceptionAgentOptions
{
    public const string SectionName = "FulfilmentExceptionAgent";

    public string Provider { get; set; } = "Local";

    public TimeSpan ToolTimeout { get; set; } = TimeSpan.FromSeconds(5);

    public TimeSpan ModelTimeout { get; set; } = TimeSpan.FromSeconds(20);

    /// <summary>Retries after the first attempt, for transient tool failures only.</summary>
    public int MaxToolRetries { get; set; } = 2;

    public int MaxRevisions { get; set; } = 3;

    public int MaxModelOutputCharacters { get; set; } = 50_000;

    public int MaxToolOutputCharacters { get; set; } = 256_000;

    public void Validate()
    {
        if (!string.Equals(Provider, "Local", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(Provider, "DeepSeek", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Fulfilment Exception Agent provider must be Local or DeepSeek.");
        }
    }
}

/// <summary>Invalid tool arguments; never retried.</summary>
public sealed class ToolInputException : Exception
{
    public ToolInputException(string message) : base(message)
    {
    }
}

/// <summary>A business rule rejected the tool request; never retried.</summary>
public sealed class ToolRejectedException : Exception
{
    public ToolRejectedException(string message) : base(message)
    {
    }
}

public sealed class ToolPermissionException : Exception
{
    public ToolPermissionException(string toolName)
        : base($"Tool '{toolName}' is not permitted for the {FulfilmentExceptionAgentConstants.AgentName}.")
    {
        ToolName = toolName;
    }

    public string ToolName { get; }
}

/// <summary>A tool failed after its bounded retries (or was rejected).</summary>
public sealed class FulfilmentExceptionToolException : Exception
{
    public FulfilmentExceptionToolException(
        string toolName,
        bool isTimeout,
        string message,
        bool isRejected = false)
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

/// <summary>The model returned a resolution document that fails schema checks.</summary>
public sealed class ResolutionSchemaException : Exception
{
    public ResolutionSchemaException(string message) : base(message)
    {
    }
}
