using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SEF_Project.Api.DTOs.Profile;
using SEF_Project.Api.Services.Profile;

namespace SEF_Project.Api.Controllers;

[ApiController]
[Route("api/profile")]
[Authorize]
public class ProfileController : ControllerBase
{
    private readonly IProfileService _profileService;

    public ProfileController(IProfileService profileService)
    {
        _profileService = profileService;
    }

    /// <summary>Gets the authenticated customer's profile.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ProfileResponse>> GetProfile(
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
        {
            return Unauthorized();
        }

        return Ok(await _profileService.GetProfileAsync(
            userId.Value,
            cancellationToken));
    }

    /// <summary>Updates the authenticated customer's existing user profile.</summary>
    [HttpPut]
    [ProducesResponseType(typeof(ProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ProfileResponse>> UpdateProfile(
        UpdateProfileRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
        {
            return Unauthorized();
        }

        var result = await _profileService.UpdateProfileAsync(
            userId.Value,
            request,
            cancellationToken);

        return result.Status == UpdateProfileStatus.Updated
            ? Ok(result.Profile)
            : Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Email already in use",
                detail: "Another account already uses this email address.");
    }

    /// <summary>Gets all addresses owned by the authenticated customer.</summary>
    [HttpGet("addresses")]
    [ProducesResponseType(typeof(IReadOnlyList<AddressResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<AddressResponse>>> GetAddresses(
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
        {
            return Unauthorized();
        }

        return Ok(await _profileService.GetAddressesAsync(
            userId.Value,
            cancellationToken));
    }

    /// <summary>Creates an address for the authenticated customer.</summary>
    [HttpPost("addresses")]
    [ProducesResponseType(typeof(AddressResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AddressResponse>> CreateAddress(
        CreateAddressRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
        {
            return Unauthorized();
        }

        var address = await _profileService.CreateAddressAsync(
            userId.Value,
            request,
            cancellationToken);

        return CreatedAtAction(nameof(GetAddresses), address);
    }

    /// <summary>Updates an address owned by the authenticated customer.</summary>
    [HttpPut("addresses/{id:int}")]
    [ProducesResponseType(typeof(AddressResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AddressResponse>> UpdateAddress(
        int id,
        UpdateAddressRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
        {
            return Unauthorized();
        }

        var address = await _profileService.UpdateAddressAsync(
            userId.Value,
            id,
            request,
            cancellationToken);

        return address == null
            ? Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Address not found",
                detail: "The address does not exist in your profile.")
            : Ok(address);
    }

    /// <summary>Deletes an address owned by the authenticated customer.</summary>
    [HttpDelete("addresses/{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteAddress(
        int id,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
        {
            return Unauthorized();
        }

        return await _profileService.DeleteAddressAsync(
            userId.Value,
            id,
            cancellationToken)
            ? NoContent()
            : Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Address not found",
                detail: "The address does not exist in your profile.");
    }

    private int? GetCurrentUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        return int.TryParse(claim, out var userId)
            ? userId
            : null;
    }
}
