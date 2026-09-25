using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SEF_Project.Api.DTOs.Common;
using SEF_Project.Api.DTOs.Marketing;
using SEF_Project.Api.Services.Marketing;

namespace SEF_Project.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Staff,Administrator")]
[Produces("application/json")]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
public class CampaignsController : ControllerBase
{
    private readonly ICampaignService _campaignService;

    public CampaignsController(ICampaignService campaignService)
    {
        _campaignService = campaignService;
    }

    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResponse<CampaignResponse>>> GetCampaigns(
        [FromQuery] CampaignQuery query,
        CancellationToken cancellationToken)
    {
        var response = await _campaignService.GetCampaignsAsync(
            query,
            cancellationToken);

        return Ok(response);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CampaignResponse>> GetCampaignById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var response = await _campaignService.GetCampaignByIdAsync(
            id,
            cancellationToken);

        return response is null ? NotFound() : Ok(response);
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CampaignResponse>> CreateCampaign(
        CampaignRequest request,
        CancellationToken cancellationToken)
    {
        var response = await _campaignService.CreateCampaignAsync(
            request,
            cancellationToken);

        return CreatedAtAction(
            nameof(GetCampaignById),
            new { id = response.Id },
            response);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CampaignResponse>> UpdateCampaign(
        Guid id,
        CampaignRequest request,
        CancellationToken cancellationToken)
    {
        var response = await _campaignService.UpdateCampaignAsync(
            id,
            request,
            cancellationToken);

        return response is null ? NotFound() : Ok(response);
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteCampaign(
        Guid id,
        CancellationToken cancellationToken)
    {
        var deleted = await _campaignService.DeleteCampaignAsync(
            id,
            cancellationToken);

        return deleted ? NoContent() : NotFound();
    }
}
