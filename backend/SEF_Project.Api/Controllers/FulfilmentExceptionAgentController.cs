using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SEF_Project.Api.AI.FulfilmentException;
using SEF_Project.Api.DTOs.Agents;

namespace SEF_Project.Api.Controllers;

/// <summary>
/// Fulfilment Exception Agent. Staff/Administrators start workflows and review
/// resolutions; the agent only proposes, and the approved action is executed by
/// OrderService after approval.
/// </summary>
[ApiController]
[Route("api/agents/fulfilment-exception")]
[Authorize(Roles = "Staff,Administrator")]
[Produces("application/json")]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
public class FulfilmentExceptionAgentController : ControllerBase
{
    private readonly IFulfilmentExceptionAgent _agent;

    public FulfilmentExceptionAgentController(IFulfilmentExceptionAgent agent)
    {
        _agent = agent;
    }

    [HttpPost("workflows")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<FulfilmentExceptionWorkflowResponse>> StartWorkflow(
        StartFulfilmentExceptionRequest request,
        CancellationToken cancellationToken)
    {
        var response = await _agent.StartAsync(request, cancellationToken);

        return CreatedAtAction(nameof(GetWorkflow), new { id = response.WorkflowId }, response);
    }

    [HttpGet("workflows")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<List<FulfilmentExceptionWorkflowSummary>>> ListWorkflows(
        [FromQuery] int limit = 20,
        CancellationToken cancellationToken = default) =>
        Ok(await _agent.ListAsync(limit, cancellationToken));

    [HttpGet("workflows/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FulfilmentExceptionWorkflowResponse>> GetWorkflow(
        Guid id,
        CancellationToken cancellationToken)
    {
        var response = await _agent.GetAsync(id, cancellationToken);

        return response is null ? NotFound() : Ok(response);
    }

    [HttpPost("workflows/{id:guid}/approve")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<FulfilmentExceptionWorkflowResponse>> Approve(
        Guid id,
        ReviewFulfilmentExceptionRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var response = await _agent.ApproveAsync(id, userId.Value, request.Comment, cancellationToken);

        return response is null ? NotFound() : Ok(response);
    }

    [HttpPost("workflows/{id:guid}/reject")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<FulfilmentExceptionWorkflowResponse>> Reject(
        Guid id,
        ReviewFulfilmentExceptionRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var response = await _agent.RejectAsync(id, userId.Value, request.Comment, cancellationToken);

        return response is null ? NotFound() : Ok(response);
    }

    [HttpPost("workflows/{id:guid}/revise")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<FulfilmentExceptionWorkflowResponse>> Revise(
        Guid id,
        ReviseFulfilmentExceptionRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var response = await _agent.ReviseAsync(id, userId.Value, request, cancellationToken);

        return response is null ? NotFound() : Ok(response);
    }

    private int? GetCurrentUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        return int.TryParse(claim, out var userId) ? userId : null;
    }
}
