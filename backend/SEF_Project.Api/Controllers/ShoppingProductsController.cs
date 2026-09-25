using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SEF_Project.Api.DTOs.Shopping;
using SEF_Project.Api.Services.Shopping;

namespace SEF_Project.Api.Controllers;

[ApiController]
[Route("api/shopping/products")]
public class ShoppingProductsController : ControllerBase
{
    private readonly IProductSearchService _productSearchService;

    public ShoppingProductsController(IProductSearchService productSearchService)
    {
        _productSearchService = productSearchService;
    }

    /// <summary>Searches and filters the active product catalogue.</summary>
    /// <remarks>
    /// Supports keyword and category filtering, an inclusive variant-price range,
    /// inventory availability, allow-listed sorting, and pagination. Size, colour,
    /// and collection filters will be added when their catalog models are integrated.
    /// </remarks>
    /// <response code="200">Returns the requested page of products.</response>
    /// <response code="400">The supplied query parameters are invalid.</response>
    [AllowAnonymous]
    [HttpGet]
    [ProducesResponseType(typeof(PagedProductResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedProductResponse>> Search(
        [FromQuery] ProductSearchQuery query,
        CancellationToken cancellationToken)
    {
        var response = await _productSearchService.SearchAsync(
            query,
            cancellationToken);

        return Ok(response);
    }
}
