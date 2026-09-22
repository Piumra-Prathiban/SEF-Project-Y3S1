using SEF_Project.Api.DTOs.Orders;

namespace SEF_Project.Api.Services.Orders;

public interface IOrderService
{
    Task<OrderResponse> CreateOrderAsync(
        int userId,
        CreateOrderRequest request,
        CancellationToken cancellationToken = default);
}
