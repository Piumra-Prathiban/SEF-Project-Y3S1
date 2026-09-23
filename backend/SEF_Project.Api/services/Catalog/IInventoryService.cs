using SEF_Project.Api.DTOs.Catalog;

namespace SEF_Project.Api.Services.Catalog;

public interface IInventoryService
{
    Task<List<InventoryResponseDto>> GetInventoryAsync(
        CancellationToken cancellationToken = default);

    Task<InventoryResponseDto?> GetInventoryByVariantIdAsync(
        Guid productVariantId,
        CancellationToken cancellationToken = default);

    Task<List<InventoryResponseDto>> GetLowStockAsync(
        CancellationToken cancellationToken = default);

    Task<InventoryResponseDto?> AdjustStockAsync(
        StockAdjustmentDto request,
        int? performedByUserId = null,
        CancellationToken cancellationToken = default);

    Task<List<StockTransactionResponseDto>> GetStockTransactionsAsync(
        Guid? productVariantId = null,
        CancellationToken cancellationToken = default);
}
