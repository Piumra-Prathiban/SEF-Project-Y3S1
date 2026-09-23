using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SEF_Project.Api.DTOs.Catalog;
using SEF_Project.Api.DTOs.Common;
using SEF_Project.Api.Services.Catalog;

namespace SEF_Project.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ProductsController : ControllerBase
{
    private readonly ICatalogService _catalogService;

    public ProductsController(ICatalogService catalogService)
    {
        _catalogService = catalogService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<ProductResponseDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<ProductResponseDto>>> GetProducts(
        [FromQuery] ProductQueryDto query,
        CancellationToken cancellationToken)
    {
        var products = await _catalogService.GetProductsAsync(
            query,
            cancellationToken);

        return Ok(products);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ProductResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductResponseDto>> GetProductById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var product = await _catalogService.GetProductByIdAsync(
            id,
            cancellationToken);

        return product is null ? NotFound() : Ok(product);
    }

    [HttpGet("{id:guid}/variants")]
    [ProducesResponseType(typeof(List<ProductVariantResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<ProductVariantResponseDto>>> GetProductVariants(
        Guid id,
        CancellationToken cancellationToken)
    {
        var product = await _catalogService.GetProductByIdAsync(
            id,
            cancellationToken);

        if (product is null)
        {
            return NotFound();
        }

        var variants = await _catalogService.GetVariantsAsync(
            id,
            cancellationToken);

        return Ok(variants);
    }

    [Authorize(Roles = "Staff,Administrator")]
    [HttpPost("{productId:guid}/variants")]
    [ProducesResponseType(typeof(ProductVariantResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ProductVariantResponseDto>> CreateProductVariant(
        Guid productId,
        ProductVariantCreateDto request,
        CancellationToken cancellationToken)
    {
        request.ProductId = productId;

        var variant = await _catalogService.CreateVariantAsync(
            request,
            cancellationToken);

        return CreatedAtAction(
            "GetVariantById",
            "Variants",
            new { id = variant.Id },
            variant);
    }

    [Authorize(Roles = "Staff,Administrator")]
    [HttpPost]
    [ProducesResponseType(typeof(ProductResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ProductResponseDto>> CreateProduct(
        ProductCreateDto request,
        CancellationToken cancellationToken)
    {
        var product = await _catalogService.CreateProductAsync(
            request,
            cancellationToken);

        return CreatedAtAction(
            nameof(GetProductById),
            new { id = product.Id },
            product);
    }

    [Authorize(Roles = "Staff,Administrator")]
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ProductResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ProductResponseDto>> UpdateProduct(
        Guid id,
        ProductUpdateDto request,
        CancellationToken cancellationToken)
    {
        var product = await _catalogService.UpdateProductAsync(
            id,
            request,
            cancellationToken);

        return product is null ? NotFound() : Ok(product);
    }

    [Authorize(Roles = "Staff,Administrator")]
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteProduct(
        Guid id,
        CancellationToken cancellationToken)
    {
        var deleted = await _catalogService.DeleteProductAsync(
            id,
            cancellationToken);

        return deleted ? NoContent() : NotFound();
    }
}
