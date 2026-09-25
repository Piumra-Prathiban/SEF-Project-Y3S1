using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SEF_Project.Api.DTOs.Catalog;
using SEF_Project.Api.DTOs.Common;
using SEF_Project.Api.Services.Catalog;

namespace SEF_Project.Api.Controllers;

/// <summary>
/// Procurement endpoints for raising, submitting, receiving and cancelling
/// purchase orders. This is an internal staff feature, so every action requires
/// the Staff or Administrator role.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Staff,Administrator")]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
public class PurchaseOrdersController : ControllerBase
{
    private readonly IPurchaseOrderService _purchaseOrderService;

    public PurchaseOrdersController(IPurchaseOrderService purchaseOrderService)
    {
        _purchaseOrderService = purchaseOrderService;
    }

    /// <summary>Lists purchase orders, paged and filtered by supplier and status.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<PurchaseOrderResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<PurchaseOrderResponse>>> GetPurchaseOrders(
        [FromQuery] PurchaseOrderQueryDto query,
        CancellationToken cancellationToken)
    {
        var purchaseOrders = await _purchaseOrderService.GetPurchaseOrdersAsync(
            query,
            cancellationToken);

        return Ok(purchaseOrders);
    }

    /// <summary>Returns a single purchase order with its lines and totals.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(PurchaseOrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PurchaseOrderResponse>> GetPurchaseOrderById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var purchaseOrder = await _purchaseOrderService.GetPurchaseOrderByIdAsync(
            id,
            cancellationToken);

        return purchaseOrder is null
            ? NotFound(ApiProblemDetails.NotFound(
                this,
                "Purchase order was not found."))
            : Ok(purchaseOrder);
    }

    /// <summary>Raises a new draft purchase order against a supplier.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(PurchaseOrderResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PurchaseOrderResponse>> CreatePurchaseOrder(
        PurchaseOrderCreateRequest request,
        CancellationToken cancellationToken)
    {
        var purchaseOrder = await _purchaseOrderService.CreatePurchaseOrderAsync(
            request,
            GetCurrentUserId(),
            cancellationToken);

        return CreatedAtAction(
            nameof(GetPurchaseOrderById),
            new { id = purchaseOrder.Id },
            purchaseOrder);
    }

    /// <summary>Submits a draft purchase order to the supplier.</summary>
    [HttpPost("{id:guid}/submit")]
    [ProducesResponseType(typeof(PurchaseOrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PurchaseOrderResponse>> SubmitPurchaseOrder(
        Guid id,
        CancellationToken cancellationToken)
    {
        var purchaseOrder = await _purchaseOrderService.SubmitPurchaseOrderAsync(
            id,
            GetCurrentUserId(),
            cancellationToken);

        return purchaseOrder is null
            ? NotFound(ApiProblemDetails.NotFound(
                this,
                "Purchase order was not found."))
            : Ok(purchaseOrder);
    }

    /// <summary>Receives a submitted purchase order and increases stock for every line.</summary>
    [HttpPost("{id:guid}/receive")]
    [ProducesResponseType(typeof(PurchaseOrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PurchaseOrderResponse>> ReceivePurchaseOrder(
        Guid id,
        CancellationToken cancellationToken)
    {
        var purchaseOrder = await _purchaseOrderService.ReceivePurchaseOrderAsync(
            id,
            GetCurrentUserId(),
            cancellationToken);

        return purchaseOrder is null
            ? NotFound(ApiProblemDetails.NotFound(
                this,
                "Purchase order was not found."))
            : Ok(purchaseOrder);
    }

    /// <summary>Cancels a draft or submitted purchase order.</summary>
    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType(typeof(PurchaseOrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PurchaseOrderResponse>> CancelPurchaseOrder(
        Guid id,
        CancellationToken cancellationToken)
    {
        var purchaseOrder = await _purchaseOrderService.CancelPurchaseOrderAsync(
            id,
            cancellationToken);

        return purchaseOrder is null
            ? NotFound(ApiProblemDetails.NotFound(
                this,
                "Purchase order was not found."))
            : Ok(purchaseOrder);
    }

    private int? GetCurrentUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        return int.TryParse(claim, out var userId)
            ? userId
            : null;
    }
}
