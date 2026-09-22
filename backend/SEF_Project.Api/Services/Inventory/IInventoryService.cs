using SEF_Project.Api.DTOs.Inventory;

namespace SEF_Project.Api.Services.InventoryManagement;

public interface IInventoryService
{
    Task<InventoryListResponse> GetInventoryAsync(
        InventoryQuery query,
        CancellationToken cancellationToken = default);

    Task<InventoryResponse?> GetVariantInventoryAsync(
        Guid variantId,
        CancellationToken cancellationToken = default);

    Task<InventoryResponse> AdjustStockAsync(
        StockAdjustmentRequest request,
        CancellationToken cancellationToken = default);

    Task<List<InventoryTransactionResponse>> GetTransactionsAsync(
        Guid? variantId = null,
        int limit = 50,
        CancellationToken cancellationToken = default);
}
