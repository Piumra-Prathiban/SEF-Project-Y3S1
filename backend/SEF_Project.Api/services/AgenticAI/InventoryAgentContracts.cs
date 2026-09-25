using System.Text.Json;
using SEF_Project.Api.DTOs.AgenticAI;

namespace SEF_Project.Api.Services.AgenticAI;

public static class InventoryAgentConstants
{
    public const string AgentName = "Inventory Analysis Agent";

    public static readonly IReadOnlySet<string> AllowedTools =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "GetProductInventory",
            "GetLowStockProducts",
            "GetStockHistory",
            "GetProductDetails"
        };
}

public sealed record InventoryAgentToolCall(
    string ToolName,
    JsonDocument Arguments);

public interface IInventoryAgentToolRegistry
{
    bool IsAllowed(string toolName);

    Task<JsonDocument> ExecuteAsync(
        string toolName,
        JsonDocument arguments,
        CancellationToken cancellationToken = default);
}

public interface IInventoryAnalysisModelClient
{
    Task<string> GenerateRecommendationsAsync(
        InventoryAnalysisRequestDto request,
        IReadOnlyDictionary<string, JsonDocument> toolResults,
        CancellationToken cancellationToken = default);
}

public interface IInventoryAnalysisAgentService
{
    Task<InventoryAnalysisResponseDto> AnalyzeAsync(
        InventoryAnalysisRequestDto request,
        CancellationToken cancellationToken = default);
}

public interface IInventoryAgentWorkflowService
{
    Task<InventoryAgentWorkflowResponseDto> CreateWorkflowAsync(
        InventoryAnalysisRequestDto request,
        CancellationToken cancellationToken = default);

    Task<InventoryAgentWorkflowResponseDto?> GetWorkflowAsync(
        Guid workflowId,
        CancellationToken cancellationToken = default);

    Task<InventoryAgentWorkflowResponseDto?> ApproveAsync(
        Guid workflowId,
        int reviewedByUserId,
        string? comment,
        CancellationToken cancellationToken = default);

    Task<InventoryAgentWorkflowResponseDto?> RejectAsync(
        Guid workflowId,
        int reviewedByUserId,
        string? comment,
        CancellationToken cancellationToken = default);

    Task<InventoryAgentWorkflowResponseDto?> RequestRevisionAsync(
        Guid workflowId,
        int reviewedByUserId,
        string comment,
        CancellationToken cancellationToken = default);
}
