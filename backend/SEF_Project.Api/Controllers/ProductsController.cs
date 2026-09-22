using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SEF_Project.Api.DTOs.Catalog;
using SEF_Project.Api.Services.Catalog;

namespace SEF_Project.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly ICatalogService _catalogService;

    public ProductsController(ICatalogService catalogService)
    {
        _catalogService = catalogService;
    }

    #region Products

    [HttpGet]
    public async Task<ActionResult<ProductListResponse>> GetProducts(
        [FromQuery] ProductQuery query,
        CancellationToken cancellationToken)
    {
        var isStaffOrAdmin = User.IsInRole("Staff") || User.IsInRole("Administrator");
        if (!isStaffOrAdmin && !query.IsActive.HasValue)
        {
            query.IsActive = true;
        }

        var response = await _catalogService.GetProductsAsync(query, cancellationToken);
        return Ok(response);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ProductDetailResponse>> GetProductById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var product = await _catalogService.GetProductByIdAsync(id, cancellationToken);

        return product is null ? NotFound() : Ok(product);
    }

    [HttpPost]
    [Authorize(Roles = "Staff,Administrator")]
    public async Task<ActionResult<ProductDetailResponse>> CreateProduct(
        [FromBody] CreateProductRequest request,
        CancellationToken cancellationToken)
    {
        var product = await _catalogService.CreateProductAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetProductById), new { id = product.Id }, product);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Staff,Administrator")]
    public async Task<ActionResult<ProductDetailResponse>> UpdateProduct(
        Guid id,
        [FromBody] UpdateProductRequest request,
        CancellationToken cancellationToken)
    {
        var product = await _catalogService.UpdateProductAsync(id, request, cancellationToken);
        return product is null ? NotFound() : Ok(product);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Staff,Administrator")]
    public async Task<IActionResult> DeleteProduct(
        Guid id,
        CancellationToken cancellationToken)
    {
        var success = await _catalogService.DeleteProductAsync(id, cancellationToken);
        return success ? NoContent() : NotFound();
    }

    #endregion

    #region Variants

    [HttpPost("{productId:guid}/variants")]
    [Authorize(Roles = "Staff,Administrator")]
    public async Task<ActionResult<ProductVariantResponse>> CreateVariant(
        Guid productId,
        [FromBody] CreateProductVariantRequest request,
        CancellationToken cancellationToken)
    {
        var variant = await _catalogService.CreateVariantAsync(productId, request, cancellationToken);
        return CreatedAtAction(nameof(GetProductById), new { id = productId }, variant);
    }

    [HttpPut("{productId:guid}/variants/{variantId:guid}")]
    [Authorize(Roles = "Staff,Administrator")]
    public async Task<ActionResult<ProductVariantResponse>> UpdateVariant(
        Guid productId,
        Guid variantId,
        [FromBody] UpdateProductVariantRequest request,
        CancellationToken cancellationToken)
    {
        var variant = await _catalogService.UpdateVariantAsync(productId, variantId, request, cancellationToken);
        return variant is null ? NotFound() : Ok(variant);
    }

    [HttpDelete("{productId:guid}/variants/{variantId:guid}")]
    [Authorize(Roles = "Staff,Administrator")]
    public async Task<IActionResult> DeleteVariant(
        Guid productId,
        Guid variantId,
        CancellationToken cancellationToken)
    {
        var success = await _catalogService.DeleteVariantAsync(productId, variantId, cancellationToken);
        return success ? NoContent() : NotFound();
    }

    #endregion

    #region Images

    [HttpPost("{productId:guid}/images")]
    [Authorize(Roles = "Staff,Administrator")]
    public async Task<ActionResult<ProductImageResponse>> AddImage(
        Guid productId,
        [FromBody] CreateProductImageRequest request,
        CancellationToken cancellationToken)
    {
        var image = await _catalogService.AddImageAsync(productId, request, cancellationToken);
        return CreatedAtAction(nameof(GetProductById), new { id = productId }, image);
    }

    [HttpPut("{productId:guid}/images/{imageId:guid}")]
    [Authorize(Roles = "Staff,Administrator")]
    public async Task<ActionResult<ProductImageResponse>> UpdateImage(
        Guid productId,
        Guid imageId,
        [FromBody] UpdateProductImageRequest request,
        CancellationToken cancellationToken)
    {
        var image = await _catalogService.UpdateImageAsync(productId, imageId, request, cancellationToken);
        return image is null ? NotFound() : Ok(image);
    }

    [HttpDelete("{productId:guid}/images/{imageId:guid}")]
    [Authorize(Roles = "Staff,Administrator")]
    public async Task<IActionResult> DeleteImage(
        Guid productId,
        Guid imageId,
        CancellationToken cancellationToken)
    {
        var success = await _catalogService.DeleteImageAsync(productId, imageId, cancellationToken);
        return success ? NoContent() : NotFound();
    }

    [HttpPatch("{productId:guid}/images/{imageId:guid}/primary")]
    [Authorize(Roles = "Staff,Administrator")]
    public async Task<IActionResult> SetPrimaryImage(
        Guid productId,
        Guid imageId,
        CancellationToken cancellationToken)
    {
        var success = await _catalogService.SetPrimaryImageAsync(productId, imageId, cancellationToken);
        return success ? NoContent() : NotFound();
    }

    #endregion
}
