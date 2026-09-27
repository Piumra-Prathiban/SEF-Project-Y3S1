using SEF_Project.Api.DTOs.Orders;

namespace SEF_Project.Api.Services.Orders;

public interface IReturnService
{
    Task<ReturnListResponse> GetReturnsAsync(
        int userId,
        bool canAccessAllOrders,
        ReturnQuery query,
        CancellationToken cancellationToken = default);

    Task<List<ReturnResponse>?> GetOrderReturnsAsync(
        int userId,
        bool canAccessAllOrders,
        Guid orderId,
        CancellationToken cancellationToken = default);

    Task<ReturnResponse?> GetReturnAsync(
        int userId,
        bool canAccessAllOrders,
        Guid returnId,
        CancellationToken cancellationToken = default);

    Task<ReturnResponse?> CreateReturnAsync(
        int userId,
        bool canAccessAllOrders,
        Guid orderId,
        CreateReturnRequest request,
        CancellationToken cancellationToken = default);

    Task<ReturnResponse?> UpdateStatusAsync(
        int userId,
        bool canManageReturns,
        Guid returnId,
        UpdateReturnStatusRequest request,
        CancellationToken cancellationToken = default);

    Task<ReturnResponse?> CancelReturnAsync(
        int userId,
        bool canAccessAllOrders,
        Guid returnId,
        CancelReturnRequest request,
        CancellationToken cancellationToken = default);
}
