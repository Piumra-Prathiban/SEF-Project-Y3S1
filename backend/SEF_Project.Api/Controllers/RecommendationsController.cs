using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SEF_Project.Api.DTOs.Recommendations;
using SEF_Project.Api.Services.Recommendations;

namespace SEF_Project.Api.Controllers;

[ApiController]
[Route("api/recommendations")]
[Authorize]
public class RecommendationsController : ControllerBase
{
    private readonly IRecommendationService _recommendationService;

    public RecommendationsController(IRecommendationService recommendationService)
    {
        _recommendationService = recommendationService;
    }

    /// <summary>Starts a fashion product recommendation request.</summary>
    /// <remarks>
    /// Customer identity comes from the JWT. Product prices and availability
    /// come from the server-side catalogue and inventory services. This is a
    /// domain-specific recommendation endpoint, not a general chat endpoint.
    /// </remarks>
    /// <response code="200">
    /// Returns catalogue-grounded recommendations or a structured safe-failure result.
    /// </response>
    /// <response code="400">The fashion preferences are invalid.</response>
    /// <response code="401">An active authenticated customer is required.</response>
    [HttpPost]
    [ProducesResponseType(typeof(RecommendationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<RecommendationResponse>> Start(
        RecommendationRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
        {
            return Unauthorized();
        }

        return Ok(await _recommendationService.StartAsync(
            userId.Value,
            request,
            cancellationToken));
    }

    private int? GetCurrentUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        return int.TryParse(claim, out var userId)
            ? userId
            : null;
    }
}
