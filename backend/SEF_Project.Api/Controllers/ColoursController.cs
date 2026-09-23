using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SEF_Project.Api.DTOs.Catalog;
using SEF_Project.Api.Services.Catalog;

namespace SEF_Project.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ColoursController : ControllerBase
{
    private readonly ICatalogService _catalogService;

    public ColoursController(ICatalogService catalogService)
    {
        _catalogService = catalogService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(List<ColourResponseDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<ColourResponseDto>>> GetColours(
        CancellationToken cancellationToken)
    {
        var colours = await _catalogService.GetColoursAsync(cancellationToken);
        return Ok(colours);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ColourResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ColourResponseDto>> GetColourById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var colour = await _catalogService.GetColourByIdAsync(
            id,
            cancellationToken);

        return colour is null
            ? NotFound(ApiProblemDetails.NotFound(
                this,
                "Colour was not found."))
            : Ok(colour);
    }

    [Authorize(Roles = "Staff,Administrator")]
    [HttpPost]
    [ProducesResponseType(typeof(ColourResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ColourResponseDto>> CreateColour(
        ColourCreateDto request,
        CancellationToken cancellationToken)
    {
        var colour = await _catalogService.CreateColourAsync(
            request,
            cancellationToken);

        return CreatedAtAction(
            nameof(GetColourById),
            new { id = colour.Id },
            colour);
    }

    [Authorize(Roles = "Staff,Administrator")]
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ColourResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ColourResponseDto>> UpdateColour(
        Guid id,
        ColourUpdateDto request,
        CancellationToken cancellationToken)
    {
        var colour = await _catalogService.UpdateColourAsync(
            id,
            request,
            cancellationToken);

        return colour is null
            ? NotFound(ApiProblemDetails.NotFound(
                this,
                "Colour was not found."))
            : Ok(colour);
    }

    [Authorize(Roles = "Staff,Administrator")]
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteColour(
        Guid id,
        CancellationToken cancellationToken)
    {
        var deleted = await _catalogService.DeleteColourAsync(
            id,
            cancellationToken);

        return deleted
            ? NoContent()
            : NotFound(ApiProblemDetails.NotFound(
                this,
                "Colour was not found."));
    }
}
