namespace SEF_Project.Api.DTOs.Agents;

/// <summary>
/// The only output the resolution model may produce. It is parsed strictly and
/// validated deterministically before anyone sees it.
/// </summary>
public class FulfilmentExceptionDocument
{
    public string SchemaVersion { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    public FulfilmentResolution Resolution { get; set; } = new();
}

public class FulfilmentResolution
{
    /// <summary>"Order", "Payment", "Shipment" or "None".</summary>
    public string TargetType { get; set; } = "None";

    public Guid? TargetId { get; set; }

    /// <summary>The proposed next status, e.g. "Confirmed", "Shipped", or "NoAction".</summary>
    public string Action { get; set; } = "NoAction";

    public string Rationale { get; set; } = string.Empty;

    public FulfilmentResolutionEvidence Evidence { get; set; } = new();
}

public class FulfilmentResolutionEvidence
{
    public string? CurrentStatus { get; set; }
}
