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

    private int? GetCurrentUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        return int.TryParse(claim, out var userId)
            ? userId
            : null;
    }
}
