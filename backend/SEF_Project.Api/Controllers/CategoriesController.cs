using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SEF_Project.Api.DTOs.Catalog;
using SEF_Project.Api.Services.Catalog;

namespace SEF_Project.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CategoriesController : ControllerBase
{
    private readonly ICatalogService _catalogService;

    public CategoriesController(ICatalogService catalogService)
    {
        _catalogService = catalogService;
    }

    [AllowAnonymous]
    [HttpGet]
    [ProducesResponseType(typeof(List<CategoryResponseDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<CategoryResponseDto>>> GetCategories(
        CancellationToken cancellationToken)
    {
        var categories = await _catalogService.GetCategoriesAsync(cancellationToken);
        return Ok(categories);
    }

    [AllowAnonymous]
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(CategoryResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CategoryResponseDto>> GetCategoryById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var category = await _catalogService.GetCategoryByIdAsync(
            id,
            cancellationToken);

        return category is null ? NotFound() : Ok(category);
    }

    [Authorize(Roles = "Staff,Administrator")]
    [HttpPost]
    [ProducesResponseType(typeof(CategoryResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CategoryResponseDto>> CreateCategory(
        CategoryCreateDto request,
        CancellationToken cancellationToken)
    {
        var category = await _catalogService.CreateCategoryAsync(
            request,
            cancellationToken);

        return CreatedAtAction(
            nameof(GetCategoryById),
            new { id = category.Id },
            category);
    }

    [Authorize(Roles = "Staff,Administrator")]
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(CategoryResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CategoryResponseDto>> UpdateCategory(
        Guid id,
        CategoryUpdateDto request,
        CancellationToken cancellationToken)
    {
        var category = await _catalogService.UpdateCategoryAsync(
            id,
            request,
            cancellationToken);

        return category is null ? NotFound() : Ok(category);
    }

    [Authorize(Roles = "Staff,Administrator")]
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteCategory(
        Guid id,
        CancellationToken cancellationToken)
    {
        var deleted = await _catalogService.DeleteCategoryAsync(
            id,
            cancellationToken);

        return deleted ? NoContent() : NotFound();
    }
}
