using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SEF_Project.Api.DTOs.Catalog;
using SEF_Project.Api.Services.Catalog;

namespace SEF_Project.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CategoriesController : ControllerBase
{
    private readonly ICatalogService _catalogService;

    public CategoriesController(ICatalogService catalogService)
    {
        _catalogService = catalogService;
    }

    [HttpGet]
    public async Task<ActionResult<List<CategoryResponse>>> GetCategories(
        [FromQuery] bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        var isStaffOrAdmin = User.IsInRole("Staff") || User.IsInRole("Administrator");
        var categories = await _catalogService.GetCategoriesAsync(
            isStaffOrAdmin && includeInactive,
            cancellationToken);

        return Ok(categories);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CategoryResponse>> GetCategoryById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var category = await _catalogService.GetCategoryByIdAsync(id, cancellationToken);

        return category is null ? NotFound() : Ok(category);
    }

    [HttpPost]
    [Authorize(Roles = "Staff,Administrator")]
    public async Task<ActionResult<CategoryResponse>> CreateCategory(
        [FromBody] CreateCategoryRequest request,
        CancellationToken cancellationToken)
    {
        var category = await _catalogService.CreateCategoryAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetCategoryById), new { id = category.Id }, category);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Staff,Administrator")]
    public async Task<ActionResult<CategoryResponse>> UpdateCategory(
        Guid id,
        [FromBody] UpdateCategoryRequest request,
        CancellationToken cancellationToken)
    {
        var category = await _catalogService.UpdateCategoryAsync(id, request, cancellationToken);
        return category is null ? NotFound() : Ok(category);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Staff,Administrator")]
    public async Task<IActionResult> DeleteCategory(
        Guid id,
        CancellationToken cancellationToken)
    {
        var success = await _catalogService.DeleteCategoryAsync(id, cancellationToken);
        return success ? NoContent() : NotFound();
    }
}
