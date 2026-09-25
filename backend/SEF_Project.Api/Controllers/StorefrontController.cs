using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SEF_Project.Api.DTOs.Storefront;
using SEF_Project.Api.Services.Storefront;

namespace SEF_Project.Api.Controllers;

/// <summary>
/// Anonymous, read-only endpoints that back the public Clothic storefront.
/// Staff catalog endpoints remain behind authentication; nothing here exposes
/// supplier details, stock counts, SKUs or inactive records.
/// </summary>
[ApiController]
[Route("api/storefront")]
[AllowAnonymous]
public class StorefrontController : ControllerBase
{
    private readonly IStorefrontService _storefrontService;

    public StorefrontController(IStorefrontService storefrontService)
    {
        _storefrontService = storefrontService;
    }

    [HttpGet("products")]
    [ProducesResponseType(
        typeof(List<StorefrontProductResponseDto>),
        StatusCodes.Status200OK)]
    public async Task<ActionResult<List<StorefrontProductResponseDto>>> GetProducts(
        [FromQuery] StorefrontProductQueryDto query,
        CancellationToken cancellationToken)
    {
        var products = await _storefrontService.GetProductsAsync(
            query,
            cancellationToken);

        return Ok(products);
    }

    [HttpGet("products/{id:guid}")]
    [ProducesResponseType(
        typeof(StorefrontProductResponseDto),
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StorefrontProductResponseDto>> GetProductById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var product = await _storefrontService.GetProductByIdAsync(
            id,
            cancellationToken);

        return product is null ? NotFound() : Ok(product);
    }
}
