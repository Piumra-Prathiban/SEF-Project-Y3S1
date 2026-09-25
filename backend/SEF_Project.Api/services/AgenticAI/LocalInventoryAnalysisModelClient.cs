using System.Text.Json;
using SEF_Project.Api.DTOs.AgenticAI;

namespace SEF_Project.Api.Services.AgenticAI;

public class LocalInventoryAnalysisModelClient : IInventoryAnalysisModelClient
{
    public Task<string> GenerateRecommendationsAsync(
        InventoryAnalysisRequestDto request,
        IReadOnlyDictionary<string, JsonDocument> toolResults,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var inventoryItems = ReadInventoryItems(toolResults);

        var output = new InventoryAgentOutputDto
        {
            Recommendations = inventoryItems
                .Select(item => new InventoryRecommendationDto
                {
                    VariantId = item.VariantId,
                    Sku = item.Sku,
                    ProductName = item.ProductName,
                    CurrentStock = item.CurrentStock,
                    ReorderLevel = item.ReorderLevel,
                    RecommendedAction = item.CurrentStock <= item.ReorderLevel
                        ? "RESTOCK"
                        : item.CurrentStock <= item.ReorderLevel + 5
                            ? "MONITOR"
                            : "NO_ACTION",
                    RecommendedQuantity = item.CurrentStock <= item.ReorderLevel
                        ? Math.Max(item.ReorderLevel - item.CurrentStock + 10, 1)
                        : 0,
                    Reason = item.CurrentStock <= item.ReorderLevel
                        ? "Current stock is at or below reorder level."
                        : item.CurrentStock <= item.ReorderLevel + 5
                            ? "Current stock is close to reorder level."
                            : "Current stock is above reorder level."
                })
                .ToList()
        };

        return Task.FromResult(JsonSerializer.Serialize(output));
    }

    private static List<InventoryContextItem> ReadInventoryItems(
        IReadOnlyDictionary<string, JsonDocument> toolResults)
    {
        if (!toolResults.TryGetValue("GetProductInventory", out var inventory)
            && !toolResults.TryGetValue("GetLowStockProducts", out inventory))
        {
            return new List<InventoryContextItem>();
        }

        if (!inventory.RootElement.TryGetProperty("inventory", out var items)
            || items.ValueKind != JsonValueKind.Array)
        {
            return new List<InventoryContextItem>();
        }

        return items.EnumerateArray()
            .Select(item => new InventoryContextItem(
                item.GetProperty("variantId").GetGuid(),
                item.GetProperty("sku").GetString() ?? string.Empty,
                item.GetProperty("productName").GetString() ?? string.Empty,
                item.GetProperty("currentStock").GetInt32(),
                item.GetProperty("reorderLevel").GetInt32()))
            .ToList();
    }

    private sealed record InventoryContextItem(
        Guid VariantId,
        string Sku,
        string ProductName,
        int CurrentStock,
        int ReorderLevel);
}
