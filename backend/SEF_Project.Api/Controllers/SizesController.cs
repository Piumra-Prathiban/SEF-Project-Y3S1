using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SEF_Project.Api.DTOs.Catalog;
using SEF_Project.Api.Services.Catalog;

namespace SEF_Project.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SizesController : ControllerBase
{
    private readonly ICatalogService _catalogService;

    public SizesController(ICatalogService catalogService)
    {
        _catalogService = catalogService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(List<SizeResponseDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<SizeResponseDto>>> GetSizes(
        CancellationToken cancellationToken)
    {
        var sizes = await _catalogService.GetSizesAsync(cancellationToken);
        return Ok(sizes);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(SizeResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SizeResponseDto>> GetSizeById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var size = await _catalogService.GetSizeByIdAsync(id, cancellationToken);

        return size is null
            ? NotFound(ApiProblemDetails.NotFound(
                this,
                "Size was not found."))
            : Ok(size);
    }

    [Authorize(Roles = "Staff,Administrator")]
    [HttpPost]
    [ProducesResponseType(typeof(SizeResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SizeResponseDto>> CreateSize(
        SizeCreateDto request,
        CancellationToken cancellationToken)
    {
        var size = await _catalogService.CreateSizeAsync(
            request,
            cancellationToken);

        return CreatedAtAction(nameof(GetSizeById), new { id = size.Id }, size);
    }

    [Authorize(Roles = "Staff,Administrator")]
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(SizeResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SizeResponseDto>> UpdateSize(
        Guid id,
        SizeUpdateDto request,
        CancellationToken cancellationToken)
    {
        var size = await _catalogService.UpdateSizeAsync(
            id,
            request,
            cancellationToken);

        return size is null
            ? NotFound(ApiProblemDetails.NotFound(
                this,
                "Size was not found."))
            : Ok(size);
    }

    [Authorize(Roles = "Staff,Administrator")]
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteSize(
        Guid id,
        CancellationToken cancellationToken)
    {
        var deleted = await _catalogService.DeleteSizeAsync(
            id,
            cancellationToken);

        return deleted
            ? NoContent()
            : NotFound(ApiProblemDetails.NotFound(
                this,
                "Size was not found."));
    }
}
