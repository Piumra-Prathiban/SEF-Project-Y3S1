using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SEF_Project.Api.DTOs.Orders;
using SEF_Project.Api.Services.Orders;

namespace SEF_Project.Api.Controllers;

[ApiController]
[Route("api/returns")]
[Authorize]
public class ReturnsController : ControllerBase
{
    private readonly IReturnService _returnService;

    public ReturnsController(IReturnService returnService)
    {
        _returnService = returnService;
    }

    [HttpGet]
    public async Task<ActionResult<ReturnListResponse>> GetReturns(
        [FromQuery] ReturnQuery query,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        return Ok(await _returnService.GetReturnsAsync(
            userId.Value,
            CanAccessAllOrders(),
            query,
            cancellationToken));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ReturnResponse>> GetReturn(
        Guid id,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var response = await _returnService.GetReturnAsync(
            userId.Value,
            CanAccessAllOrders(),
            id,
            cancellationToken);
        return response is null ? NotFound() : Ok(response);
    }

    [HttpGet("~/api/orders/{orderId:guid}/returns")]
    public async Task<ActionResult<List<ReturnResponse>>> GetOrderReturns(
        Guid orderId,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var response = await _returnService.GetOrderReturnsAsync(
            userId.Value,
            CanAccessAllOrders(),
            orderId,
            cancellationToken);
        return response is null ? NotFound() : Ok(response);
    }

    [HttpPost("~/api/orders/{orderId:guid}/returns")]
    [Authorize(Roles = "Customer")]
    public async Task<ActionResult<ReturnResponse>> CreateReturn(
        Guid orderId,
        CreateReturnRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var response = await _returnService.CreateReturnAsync(
            userId.Value,
            canAccessAllOrders: false,
            orderId,
            request,
            cancellationToken);
        return response is null ? NotFound() : Ok(response);
    }

    [HttpPatch("{id:guid}/status")]
    [Authorize(Roles = "Staff,Administrator")]
    public async Task<ActionResult<ReturnResponse>> UpdateStatus(
        Guid id,
        UpdateReturnStatusRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var response = await _returnService.UpdateStatusAsync(
            userId.Value,
            canManageReturns: true,
            id,
            request,
            cancellationToken);
        return response is null ? NotFound() : Ok(response);
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<ActionResult<ReturnResponse>> CancelReturn(
        Guid id,
        CancelReturnRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var response = await _returnService.CancelReturnAsync(
            userId.Value,
            CanAccessAllOrders(),
            id,
            request,
            cancellationToken);
        return response is null ? NotFound() : Ok(response);
    }

    private int? GetCurrentUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(claim, out var userId) ? userId : null;
    }

    private bool CanAccessAllOrders() =>
        User.IsInRole("Staff") || User.IsInRole("Administrator");
}
