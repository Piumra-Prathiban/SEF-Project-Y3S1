using SEF_Project.Api.DTOs.Catalog;
using SEF_Project.Api.DTOs.Common;

namespace SEF_Project.Api.Services.Catalog;

/// <summary>
/// Staff-facing procurement workflow. A purchase order is raised as a
/// <c>Draft</c> against a supplier, submitted to the supplier, and finally
/// received, which increases stock through the shared inventory ledger.
/// Invalid status transitions are rejected with
/// <see cref="InvalidOperationException"/> so the API can surface a conflict.
/// </summary>
public interface IPurchaseOrderService
{
    /// <summary>
    /// Lists purchase orders, newest first, paged and filtered by supplier
    /// and/or status. Each item includes its lines and computed totals.
    /// </summary>
    Task<PagedResponse<PurchaseOrderResponse>> GetPurchaseOrdersAsync(
        PurchaseOrderQueryDto query,
        CancellationToken cancellationToken = default);

    /// <summary>Returns a single purchase order or <c>null</c> when it does not exist.</summary>
    Task<PurchaseOrderResponse?> GetPurchaseOrderByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Raises a new draft purchase order. Throws <see cref="ArgumentException"/>
    /// when the supplier or any product variant does not exist, the supplier is
    /// inactive, or a line carries an invalid quantity/unit cost.
    /// </summary>
    Task<PurchaseOrderResponse> CreatePurchaseOrderAsync(
        PurchaseOrderCreateRequest request,
        int? performedByUserId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Moves a draft purchase order to <c>Submitted</c> and stamps
    /// <c>SubmittedAt</c>. Returns <c>null</c> when the order does not exist and
    /// throws <see cref="InvalidOperationException"/> for an invalid transition.
    /// </summary>
    Task<PurchaseOrderResponse?> SubmitPurchaseOrderAsync(
        Guid id,
        int? performedByUserId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Moves a submitted purchase order to <c>Received</c>, stamps
    /// <c>ReceivedAt</c> and posts a stock increase for every line through the
    /// inventory ledger. The whole receipt is transactional: if any line fails
    /// it rolls back. Returns <c>null</c> when the order does not exist and
    /// throws <see cref="InvalidOperationException"/> for an invalid transition
    /// (so a second receipt is impossible).
    /// </summary>
    Task<PurchaseOrderResponse?> ReceivePurchaseOrderAsync(
        Guid id,
        int? performedByUserId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels a draft or submitted purchase order. Returns <c>null</c> when the
    /// order does not exist and throws <see cref="InvalidOperationException"/>
    /// when it can no longer be cancelled (for example once received).
    /// </summary>
    Task<PurchaseOrderResponse?> CancelPurchaseOrderAsync(
        Guid id,
        CancellationToken cancellationToken = default);
}
