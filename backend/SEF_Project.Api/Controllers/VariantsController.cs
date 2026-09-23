using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SEF_Project.Api.DTOs.Catalog;
using SEF_Project.Api.Services.Catalog;

namespace SEF_Project.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class VariantsController : ControllerBase
{
    private readonly ICatalogService _catalogService;

    public VariantsController(ICatalogService catalogService)
    {
        _catalogService = catalogService;
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ProductVariantResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductVariantResponseDto>> GetVariantById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var variant = await _catalogService.GetVariantByIdAsync(
            id,
            cancellationToken);

        return variant is null ? NotFound() : Ok(variant);
    }

    [Authorize(Roles = "Staff,Administrator")]
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ProductVariantResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ProductVariantResponseDto>> UpdateVariant(
        Guid id,
        ProductVariantUpdateDto request,
        CancellationToken cancellationToken)
    {
        var variant = await _catalogService.UpdateVariantAsync(
            id,
            request,
            cancellationToken);

        return variant is null ? NotFound() : Ok(variant);
    }

    [Authorize(Roles = "Staff,Administrator")]
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteVariant(
        Guid id,
        CancellationToken cancellationToken)
    {
        var deleted = await _catalogService.DeleteVariantAsync(
            id,
            cancellationToken);

        return deleted ? NoContent() : NotFound();
    }
}
