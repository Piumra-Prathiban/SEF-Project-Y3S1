using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SEF_Project.Api.DTOs.Catalog;
using SEF_Project.Api.Services.Catalog;

namespace SEF_Project.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CollectionsController : ControllerBase
{
    private readonly ICatalogService _catalogService;

    public CollectionsController(ICatalogService catalogService)
    {
        _catalogService = catalogService;
    }

    [AllowAnonymous]
    [HttpGet]
    [ProducesResponseType(typeof(List<CollectionResponseDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<CollectionResponseDto>>> GetCollections(
        CancellationToken cancellationToken)
    {
        var collections = await _catalogService.GetCollectionsAsync(cancellationToken);
        return Ok(collections);
    }

    [AllowAnonymous]
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(CollectionResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CollectionResponseDto>> GetCollectionById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var collection = await _catalogService.GetCollectionByIdAsync(
            id,
            cancellationToken);

        return collection is null ? NotFound() : Ok(collection);
    }

    [Authorize(Roles = "Staff,Administrator")]
    [HttpPost]
    [ProducesResponseType(typeof(CollectionResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CollectionResponseDto>> CreateCollection(
        CollectionCreateDto request,
        CancellationToken cancellationToken)
    {
        var collection = await _catalogService.CreateCollectionAsync(
            request,
            cancellationToken);

        return CreatedAtAction(
            nameof(GetCollectionById),
            new { id = collection.Id },
            collection);
    }

    [Authorize(Roles = "Staff,Administrator")]
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(CollectionResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CollectionResponseDto>> UpdateCollection(
        Guid id,
        CollectionUpdateDto request,
        CancellationToken cancellationToken)
    {
        var collection = await _catalogService.UpdateCollectionAsync(
            id,
            request,
            cancellationToken);

        return collection is null ? NotFound() : Ok(collection);
    }

    [Authorize(Roles = "Staff,Administrator")]
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteCollection(
        Guid id,
        CancellationToken cancellationToken)
    {
        var deleted = await _catalogService.DeleteCollectionAsync(
            id,
            cancellationToken);

        return deleted ? NoContent() : NotFound();
    }
}
