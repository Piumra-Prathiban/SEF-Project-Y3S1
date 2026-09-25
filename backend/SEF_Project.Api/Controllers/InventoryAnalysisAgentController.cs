using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SEF_Project.Api.DTOs.AgenticAI;
using SEF_Project.Api.Services.AgenticAI;

namespace SEF_Project.Api.Controllers;

[ApiController]
[Route("api/agents/inventory-analysis")]
[Authorize(Roles = "Staff,Administrator")]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
public class InventoryAnalysisAgentController : ControllerBase
{
    private readonly IInventoryAnalysisAgentService _agentService;

    public InventoryAnalysisAgentController(
        IInventoryAnalysisAgentService agentService)
    {
        _agentService = agentService;
    }

    [HttpPost]
    [ProducesResponseType(typeof(InventoryAnalysisResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<InventoryAnalysisResponseDto>> AnalyzeInventory(
        InventoryAnalysisRequestDto request,
        CancellationToken cancellationToken)
    {
        var response = await _agentService.AnalyzeAsync(
            request,
            cancellationToken);

        return Ok(response);
    }
}
