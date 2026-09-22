using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SEF_Project.Api.DTOs.Orders;
using SEF_Project.Api.Services.Orders;

namespace SEF_Project.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class OrdersController : ControllerBase
{
    private readonly IOrderService _orderService;

    public OrdersController(IOrderService orderService)
    {
        _orderService = orderService;
    }

    [HttpPost]
    public async Task<ActionResult<OrderResponse>> CreateOrder(
        CreateOrderRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
        {
            return Unauthorized();
        }

        var response = await _orderService.CreateOrderAsync(
            userId.Value,
            request,
            cancellationToken);

        return Ok(response);
    }

    [HttpGet]
    public async Task<ActionResult<OrderListResponse>> GetOrders(
        [FromQuery] OrderQuery query,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
        {
            return Unauthorized();
        }

        var response = await _orderService.GetOrdersAsync(
            userId.Value,
            CanAccessAllOrders(),
            query,
            cancellationToken);

        return Ok(response);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<OrderResponse>> GetOrderById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
        {
            return Unauthorized();
        }

        var response = await _orderService.GetOrderByIdAsync(
            userId.Value,
            CanAccessAllOrders(),
            id,
            cancellationToken);

        return response is null ? NotFound() : Ok(response);
    }

    [HttpGet("{id:guid}/status-history")]
    public async Task<ActionResult<List<OrderStatusHistoryResponse>>>
        GetOrderStatusHistory(
            Guid id,
            CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
        {
            return Unauthorized();
        }

        var history = await _orderService.GetOrderStatusHistoryAsync(
            userId.Value,
            CanAccessAllOrders(),
            id,
            cancellationToken);

        return history is null ? NotFound() : Ok(history);
    }

    [HttpPatch("{id:guid}/status")]
    [Authorize(Roles = "Staff,Administrator")]
    public async Task<ActionResult<OrderResponse>> UpdateOrderStatus(
        Guid id,
        UpdateOrderStatusRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
        {
            return Unauthorized();
        }

        var response = await _orderService.UpdateOrderStatusAsync(
            userId.Value,
            CanAccessAllOrders(),
            id,
            request,
            cancellationToken);

        return response is null ? NotFound() : Ok(response);
    }

    [HttpPost("{id:guid}/payments")]
    public async Task<ActionResult<PaymentResponse>> CreatePayment(
        Guid id,
        CreatePaymentRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
        {
            return Unauthorized();
        }

        var response = await _orderService.CreatePaymentAsync(
            userId.Value,
            CanAccessAllOrders(),
            id,
            request,
            cancellationToken);

        return response is null ? NotFound() : Ok(response);
    }

    [HttpGet("{id:guid}/payments")]
    public async Task<ActionResult<List<PaymentResponse>>> GetPayments(
        Guid id,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
        {
            return Unauthorized();
        }

        var payments = await _orderService.GetPaymentsAsync(
            userId.Value,
            CanAccessAllOrders(),
            id,
            cancellationToken);

        return payments is null ? NotFound() : Ok(payments);
    }

    [HttpPatch("{id:guid}/payments/{paymentId:guid}/status")]
    [Authorize(Roles = "Staff,Administrator")]
    public async Task<ActionResult<PaymentResponse>> UpdatePaymentStatus(
        Guid id,
        Guid paymentId,
        UpdatePaymentStatusRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
        {
            return Unauthorized();
        }

        var response = await _orderService.UpdatePaymentStatusAsync(
            userId.Value,
            CanAccessAllOrders(),
            id,
            paymentId,
            request,
            cancellationToken);

        return response is null ? NotFound() : Ok(response);
    }

    private int? GetCurrentUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        return int.TryParse(claim, out var userId)
            ? userId
            : null;
    }

    private bool CanAccessAllOrders() =>
        User.IsInRole("Staff") || User.IsInRole("Administrator");
}
