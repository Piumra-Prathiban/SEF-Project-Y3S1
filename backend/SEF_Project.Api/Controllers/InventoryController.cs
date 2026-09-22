using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SEF_Project.Api.DTOs.Inventory;
using SEF_Project.Api.Services.InventoryManagement;

namespace SEF_Project.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Staff,Administrator")]
public class InventoryController : ControllerBase
{
    private readonly IInventoryService _inventoryService;

    public InventoryController(IInventoryService inventoryService)
    {
        _inventoryService = inventoryService;
    }

    [HttpGet]
    public async Task<ActionResult<InventoryListResponse>> GetInventory(
        [FromQuery] InventoryQuery query,
        CancellationToken cancellationToken)
    {
        var response = await _inventoryService.GetInventoryAsync(query, cancellationToken);
        return Ok(response);
    }

    [HttpGet("{variantId:guid}")]
    public async Task<ActionResult<InventoryResponse>> GetVariantInventory(
        Guid variantId,
        CancellationToken cancellationToken)
    {
        var inventory = await _inventoryService.GetVariantInventoryAsync(variantId, cancellationToken);
        return inventory is null ? NotFound() : Ok(inventory);
    }

    [HttpPost("adjust")]
    public async Task<ActionResult<InventoryResponse>> AdjustStock(
        [FromBody] StockAdjustmentRequest request,
        CancellationToken cancellationToken)
    {
        var response = await _inventoryService.AdjustStockAsync(request, cancellationToken);
        return Ok(response);
    }

    [HttpGet("transactions")]
    public async Task<ActionResult<List<InventoryTransactionResponse>>> GetTransactions(
        [FromQuery] int limit = 50,
        CancellationToken cancellationToken = default)
    {
        var transactions = await _inventoryService.GetTransactionsAsync(null, limit, cancellationToken);
        return Ok(transactions);
    }

    [HttpGet("{variantId:guid}/transactions")]
    public async Task<ActionResult<List<InventoryTransactionResponse>>> GetVariantTransactions(
        Guid variantId,
        [FromQuery] int limit = 50,
        CancellationToken cancellationToken = default)
    {
        var transactions = await _inventoryService.GetTransactionsAsync(variantId, limit, cancellationToken);
        return Ok(transactions);
    }
}
