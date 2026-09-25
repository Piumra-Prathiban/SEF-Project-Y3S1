using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SEF_Project.Api.DTOs.Catalog;
using SEF_Project.Api.Services.Catalog;

namespace SEF_Project.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
public class SuppliersController : ControllerBase
{
    private readonly ISupplierService _supplierService;

    public SuppliersController(ISupplierService supplierService)
    {
        _supplierService = supplierService;
    }

    /// <summary>Lists suppliers, optionally searched and filtered by status.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<SupplierResponseDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<SupplierResponseDto>>> GetSuppliers(
        [FromQuery] SupplierQueryDto query,
        CancellationToken cancellationToken)
    {
        var suppliers = await _supplierService.GetSuppliersAsync(query, cancellationToken);
        return Ok(suppliers);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(SupplierResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SupplierResponseDto>> GetSupplierById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var supplier = await _supplierService.GetSupplierByIdAsync(
            id,
            cancellationToken);

        return supplier is null
            ? NotFound(ApiProblemDetails.NotFound(
                this,
                "Supplier was not found."))
            : Ok(supplier);
    }

    [Authorize(Roles = "Staff,Administrator")]
    [HttpPost]
    [ProducesResponseType(typeof(SupplierResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SupplierResponseDto>> CreateSupplier(
        SupplierCreateDto request,
        CancellationToken cancellationToken)
    {
        var supplier = await _supplierService.CreateSupplierAsync(
            request,
            cancellationToken);

        return CreatedAtAction(
            nameof(GetSupplierById),
            new { id = supplier.Id },
            supplier);
    }

    [Authorize(Roles = "Staff,Administrator")]
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(SupplierResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SupplierResponseDto>> UpdateSupplier(
        Guid id,
        SupplierUpdateDto request,
        CancellationToken cancellationToken)
    {
        var supplier = await _supplierService.UpdateSupplierAsync(
            id,
            request,
            cancellationToken);

        return supplier is null
            ? NotFound(ApiProblemDetails.NotFound(
                this,
                "Supplier was not found."))
            : Ok(supplier);
    }

    [Authorize(Roles = "Staff,Administrator")]
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> DeleteSupplier(
        Guid id,
        CancellationToken cancellationToken)
    {
        var deleted = await _supplierService.DeleteSupplierAsync(
            id,
            cancellationToken);

        return deleted
            ? NoContent()
            : NotFound(ApiProblemDetails.NotFound(
                this,
                "Supplier was not found."));
    }
}
