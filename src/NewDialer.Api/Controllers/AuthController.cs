using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NewDialer.Api.Security;
using NewDialer.Application.Abstractions;
using NewDialer.Contracts.Auth;

namespace NewDialer.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(
    IAuthenticationService authenticationService,
    IAgentActivityService agentActivityService) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("admin-signup")]
    public async Task<ActionResult<SessionDto>> AdminSignup([FromBody] AdminSignupRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var session = await authenticationService.RegisterAdminAsync(request, cancellationToken);
            await agentActivityService.RecordSignInAsync(session.TenantId, session.UserId, session.Role, cancellationToken);
            return Ok(session);
        }
        catch (InvalidOperationException exception)
        {
            return exception.Message.Contains("already exists", StringComparison.OrdinalIgnoreCase)
                ? Conflict(CreateProblemDetails("Signup failed", exception.Message, StatusCodes.Status409Conflict))
                : BadRequest(CreateProblemDetails("Signup failed", exception.Message, StatusCodes.Status400BadRequest));
        }
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult<SessionDto>> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var session = await authenticationService.LoginAsync(request, cancellationToken);
            if (session is null)
            {
                return Unauthorized(CreateProblemDetails("Login failed", "Invalid credentials or disabled account.", StatusCodes.Status401Unauthorized));
            }

            await agentActivityService.RecordSignInAsync(session.TenantId, session.UserId, session.Role, cancellationToken);
            return Ok(session);
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(CreateProblemDetails("Login failed", exception.Message, StatusCodes.Status400BadRequest));
        }
    }

    [Authorize]
    [HttpGet("me")]
    public ActionResult<object> Me()
    {
        return Ok(new
        {
            UserId = User.FindFirstValue(ClaimTypes.NameIdentifier),
            Name = User.FindFirstValue(ClaimTypes.Name),
            Email = User.FindFirstValue(ClaimTypes.Email),
            Role = User.FindFirstValue(ClaimTypes.Role),
            TenantId = User.FindFirstValue("tenant_id"),
            WorkspaceKey = User.FindFirstValue("workspace_key"),
        });
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        await agentActivityService.RecordSignOutAsync(
            User.GetRequiredTenantId(),
            User.GetRequiredUserId(),
            User.GetRequiredRole(),
            cancellationToken);

        return NoContent();
    }

    private ProblemDetails CreateProblemDetails(string title, string detail, int statusCode)
    {
        return new ProblemDetails
        {
            Title = title,
            Detail = detail,
            Status = statusCode,
        };
    }
}
