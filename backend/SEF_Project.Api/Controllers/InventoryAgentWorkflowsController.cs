using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SEF_Project.Api.DTOs.AgenticAI;
using SEF_Project.Api.Services.AgenticAI;

namespace SEF_Project.Api.Controllers;

[ApiController]
[Route("api/inventory/agent/workflows")]
[Authorize(Roles = "Staff,Administrator")]
public class InventoryAgentWorkflowsController : ControllerBase
{
    private readonly IInventoryAgentWorkflowService _workflowService;

    public InventoryAgentWorkflowsController(
        IInventoryAgentWorkflowService workflowService)
    {
        _workflowService = workflowService;
    }

    [HttpPost]
    [ProducesResponseType(typeof(InventoryAgentWorkflowResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<InventoryAgentWorkflowResponseDto>> CreateWorkflow(
        InventoryAnalysisRequestDto request,
        CancellationToken cancellationToken)
    {
        var response = await _workflowService.CreateWorkflowAsync(
            request,
            cancellationToken);

        return CreatedAtAction(
            nameof(GetWorkflow),
            new { id = response.WorkflowId },
            response);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(InventoryAgentWorkflowResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<InventoryAgentWorkflowResponseDto>> GetWorkflow(
        Guid id,
        CancellationToken cancellationToken)
    {
        var workflow = await _workflowService.GetWorkflowAsync(
            id,
            cancellationToken);

        return workflow is null
            ? NotFound(ApiProblemDetails.NotFound(
                this,
                "Inventory agent workflow was not found."))
            : Ok(workflow);
    }

    [HttpPost("{id:guid}/approve")]
    [ProducesResponseType(typeof(InventoryAgentWorkflowResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<InventoryAgentWorkflowResponseDto>> Approve(
        Guid id,
        InventoryAgentApprovalRequestDto request,
        CancellationToken cancellationToken)
    {
        var workflow = await _workflowService.ApproveAsync(
            id,
            GetCurrentUserId(),
            request.Comment,
            cancellationToken);

        return workflow is null
            ? NotFound(ApiProblemDetails.NotFound(
                this,
                "Inventory agent workflow was not found."))
            : Ok(workflow);
    }

    [HttpPost("{id:guid}/reject")]
    [ProducesResponseType(typeof(InventoryAgentWorkflowResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<InventoryAgentWorkflowResponseDto>> Reject(
        Guid id,
        InventoryAgentApprovalRequestDto request,
        CancellationToken cancellationToken)
    {
        var workflow = await _workflowService.RejectAsync(
            id,
            GetCurrentUserId(),
            request.Comment,
            cancellationToken);

        return workflow is null
            ? NotFound(ApiProblemDetails.NotFound(
                this,
                "Inventory agent workflow was not found."))
            : Ok(workflow);
    }

    [HttpPost("{id:guid}/revise")]
    [ProducesResponseType(typeof(InventoryAgentWorkflowResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<InventoryAgentWorkflowResponseDto>> Revise(
        Guid id,
        InventoryAgentRevisionRequestDto request,
        CancellationToken cancellationToken)
    {
        var workflow = await _workflowService.RequestRevisionAsync(
            id,
            GetCurrentUserId(),
            request.Comment,
            cancellationToken);

        return workflow is null
            ? NotFound(ApiProblemDetails.NotFound(
                this,
                "Inventory agent workflow was not found."))
            : Ok(workflow);
    }

    private int GetCurrentUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (!int.TryParse(claim, out var userId))
        {
            throw new UnauthorizedAccessException(
                "Authenticated user id claim is missing.");
        }

        return userId;
    }
}
