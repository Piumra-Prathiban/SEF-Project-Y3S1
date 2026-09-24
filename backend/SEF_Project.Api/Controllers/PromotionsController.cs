using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SEF_Project.Api.DTOs.Common;
using SEF_Project.Api.DTOs.Marketing;
using SEF_Project.Api.Services.Marketing;

namespace SEF_Project.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public class PromotionsController : ControllerBase
{
    private readonly IPromotionService _promotionService;
    private readonly IPromotionPricingService _promotionPricingService;
    private readonly IPromotionOfferService _promotionOfferService;

    public PromotionsController(
        IPromotionService promotionService,
        IPromotionPricingService promotionPricingService,
        IPromotionOfferService promotionOfferService)
    {
        _promotionService = promotionService;
        _promotionPricingService = promotionPricingService;
        _promotionOfferService = promotionOfferService;
    }

    /// <summary>
    /// Lists promotions. Staff/Administrators see all promotions; everyone
    /// else (including anonymous users) only sees currently live promotions.
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResponse<PromotionResponse>>> GetPromotions(
        [FromQuery] PromotionQuery query,
        CancellationToken cancellationToken)
    {
        var response = await _promotionService.GetPromotionsAsync(
            CanManagePromotions(),
            query,
            cancellationToken);

        return Ok(response);
    }

    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PromotionResponse>> GetPromotionById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var response = await _promotionService.GetPromotionByIdAsync(
            CanManagePromotions(),
            id,
            cancellationToken);

        return response is null ? NotFound() : Ok(response);
    }

    /// <summary>
    /// Eligible products of a live promotion with server-calculated prices
    /// (customer-facing). 404 when the promotion is missing or not live.
    /// </summary>
    [HttpGet("{id:guid}/products")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PromotionProductsResponse>> GetPromotionProducts(
        Guid id,
        CancellationToken cancellationToken)
    {
        var response = await _promotionOfferService.GetPromotionProductsAsync(id, cancellationToken);

        return response is null ? NotFound() : Ok(response);
    }

    /// <summary>
    /// A product's live promotions and its variants priced with the best one
    /// (customer-facing). 404 when the product is missing or inactive.
    /// </summary>
    [HttpGet("products/{productId:guid}")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductPromotionsResponse>> GetProductPromotions(
        Guid productId,
        CancellationToken cancellationToken)
    {
        var response = await _promotionOfferService.GetProductPromotionsAsync(productId, cancellationToken);

        return response is null ? NotFound() : Ok(response);
    }

    /// <summary>Products and categories a promotion can target (for the admin UI).</summary>
    [HttpGet("targets")]
    [Authorize(Roles = "Staff,Administrator")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PromotionTargetsResponse>> GetTargetOptions(
        CancellationToken cancellationToken) =>
        Ok(await _promotionService.GetTargetOptionsAsync(cancellationToken));

    [HttpPost]
    [Authorize(Roles = "Staff,Administrator")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PromotionResponse>> CreatePromotion(
        PromotionRequest request,
        CancellationToken cancellationToken)
    {
        var response = await _promotionService.CreatePromotionAsync(
            request,
            cancellationToken);

        return CreatedAtAction(
            nameof(GetPromotionById),
            new { id = response.Id },
            response);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Staff,Administrator")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PromotionResponse>> UpdatePromotion(
        Guid id,
        PromotionRequest request,
        CancellationToken cancellationToken)
    {
        var response = await _promotionService.UpdatePromotionAsync(
            id,
            request,
            cancellationToken);

        return response is null ? NotFound() : Ok(response);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Staff,Administrator")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeletePromotion(
        Guid id,
        CancellationToken cancellationToken)
    {
        var deleted = await _promotionService.DeletePromotionAsync(
            id,
            cancellationToken);

        return deleted ? NoContent() : NotFound();
    }

    /// <summary>
    /// Calculates the discount for one product variant. Prices are read from
    /// the database; the client only supplies identifiers.
    /// </summary>
    [HttpPost("{id:guid}/calculate-discount")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PromotionDiscountResponse>> CalculateDiscount(
        Guid id,
        CalculatePromotionDiscountRequest request,
        CancellationToken cancellationToken)
    {
        var response = await _promotionPricingService
            .CalculatePromotionDiscountAsync(id, request, cancellationToken);

        return response is null ? NotFound() : Ok(response);
    }

    private bool CanManagePromotions() =>
        User.IsInRole("Staff") || User.IsInRole("Administrator");
}
