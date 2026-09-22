using SEF_Project.Api.DTOs.Orders;

namespace SEF_Project.Api.Services.Orders;

public interface IOrderService
{
    Task<OrderResponse> CreateOrderAsync(
        int userId,
        CreateOrderRequest request,
        CancellationToken cancellationToken = default);

    Task<OrderListResponse> GetOrdersAsync(
        int userId,
        bool canAccessAllOrders,
        OrderQuery query,
        CancellationToken cancellationToken = default);

    Task<OrderResponse?> GetOrderByIdAsync(
        int userId,
        bool canAccessAllOrders,
        Guid orderId,
        CancellationToken cancellationToken = default);

    Task<List<OrderStatusHistoryResponse>?> GetOrderStatusHistoryAsync(
        int userId,
        bool canAccessAllOrders,
        Guid orderId,
        CancellationToken cancellationToken = default);

    Task<OrderResponse?> UpdateOrderStatusAsync(
        int userId,
        bool canManageOrders,
        Guid orderId,
        UpdateOrderStatusRequest request,
        CancellationToken cancellationToken = default);

    Task<PaymentResponse?> CreatePaymentAsync(
        int userId,
        bool canAccessAllOrders,
        Guid orderId,
        CreatePaymentRequest request,
        CancellationToken cancellationToken = default);

    Task<List<PaymentResponse>?> GetPaymentsAsync(
        int userId,
        bool canAccessAllOrders,
        Guid orderId,
        CancellationToken cancellationToken = default);

    Task<PaymentResponse?> UpdatePaymentStatusAsync(
        int userId,
        bool canManageOrders,
        Guid orderId,
        Guid paymentId,
        UpdatePaymentStatusRequest request,
        CancellationToken cancellationToken = default);
}
