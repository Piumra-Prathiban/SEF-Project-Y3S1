using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SEF_Project.Api.DTOs.Marketing;
using SEF_Project.Api.Services.Marketing;

namespace SEF_Project.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PromotionsController : ControllerBase
{
    private readonly IPromotionPricingService _promotionPricingService;

    public PromotionsController(IPromotionPricingService promotionPricingService)
    {
        _promotionPricingService = promotionPricingService;
    }

    [HttpPost("calculate-discount")]
    public async Task<ActionResult<PromotionDiscountResponse>> CalculateDiscount(
        CalculatePromotionDiscountRequest request,
        CancellationToken cancellationToken)
    {
        var response = await _promotionPricingService
            .CalculatePromotionDiscountAsync(request, cancellationToken);

        return response is null ? NotFound() : Ok(response);
    }
}
