using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SEF_Project.Api.DTOs.Auth;
using SEF_Project.Api.Services.Auth;

namespace SEF_Project.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(
        RegisterRequest request)
    {
        try
        {
            var response = await _authService.RegisterAsync(request);
            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new
            {
                message = ex.Message
            });
        }
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(
        LoginRequest request)
    {
        try
        {
            var response = await _authService.LoginAsync(request);
            return Ok(response);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new
            {
                message = ex.Message
            });
        }
    }

    [Authorize]
    [HttpGet("me")]
    public IActionResult Me()
    {
        return Ok(new
        {
            message = "You are authenticated.",
            userId = User.FindFirst(
                System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,
            email = User.FindFirst(
                System.Security.Claims.ClaimTypes.Email)?.Value,
            role = User.FindFirst(
                System.Security.Claims.ClaimTypes.Role)?.Value
        });
    }

    [Authorize(Roles = "Administrator")]
    [HttpGet("admin-test")]
    public IActionResult AdminTest()
    {
        return Ok(new
        {
            message = "You have Administrator access."
        });
    }
}