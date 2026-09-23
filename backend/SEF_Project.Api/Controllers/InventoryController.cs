using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SEF_Project.Api.DTOs.Catalog;
using SEF_Project.Api.Services.Catalog;

namespace SEF_Project.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class InventoryController : ControllerBase
{
    private readonly IInventoryService _inventoryService;

    public InventoryController(IInventoryService inventoryService)
    {
        _inventoryService = inventoryService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(List<InventoryResponseDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<InventoryResponseDto>>> GetInventory(
        CancellationToken cancellationToken)
    {
        var inventory = await _inventoryService.GetInventoryAsync(
            cancellationToken);

        return Ok(inventory);
    }

    [HttpGet("low-stock")]
    [ProducesResponseType(typeof(List<InventoryResponseDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<InventoryResponseDto>>> GetLowStock(
        CancellationToken cancellationToken)
    {
        var inventory = await _inventoryService.GetLowStockAsync(
            cancellationToken);

        return Ok(inventory);
    }

    [HttpGet("{variantId:guid}")]
    [ProducesResponseType(typeof(InventoryResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<InventoryResponseDto>> GetInventoryByVariantId(
        Guid variantId,
        CancellationToken cancellationToken)
    {
        var inventory = await _inventoryService.GetInventoryByVariantIdAsync(
            variantId,
            cancellationToken);

        return inventory is null
            ? NotFound(ApiProblemDetails.NotFound(
                this,
                "Inventory record was not found for this product variant."))
            : Ok(inventory);
    }

    [HttpGet("{variantId:guid}/history")]
    [ProducesResponseType(typeof(List<StockTransactionResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<StockTransactionResponseDto>>> GetHistory(
        Guid variantId,
        CancellationToken cancellationToken)
    {
        var inventory = await _inventoryService.GetInventoryByVariantIdAsync(
            variantId,
            cancellationToken);

        if (inventory is null)
        {
            return NotFound(ApiProblemDetails.NotFound(
                this,
                "Inventory record was not found for this product variant."));
        }

        var history = await _inventoryService.GetStockTransactionsAsync(
            variantId,
            cancellationToken);

        return Ok(history);
    }

    [HttpPost("{variantId:guid}/adjust")]
    [Authorize(Roles = "Staff,Administrator")]
    [ProducesResponseType(typeof(InventoryResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<InventoryResponseDto>> AdjustStock(
        Guid variantId,
        StockAdjustmentDto request,
        CancellationToken cancellationToken)
    {
        request.ProductVariantId = variantId;

        var updated = await _inventoryService.AdjustStockAsync(
            request,
            GetCurrentUserId(),
            cancellationToken);

        return updated is null
            ? NotFound(ApiProblemDetails.NotFound(
                this,
                "Inventory record was not found for this product variant."))
            : Ok(updated);
    }

    private int? GetCurrentUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        return int.TryParse(claim, out var userId)
            ? userId
            : null;
    }
}
