using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NewDialer.Api.Security;
using NewDialer.Application.Abstractions;
using NewDialer.Contracts.Dialer;

namespace NewDialer.Api.Controllers;

[ApiController]
[Route("api/dialer")]
[Authorize(Roles = "Admin,Agent")]
public sealed class DialerController(
    IAgentActivityService agentActivityService) : ControllerBase
{
    [HttpPost("call/start")]
    public async Task<ActionResult<StartCallResponse>> StartCall([FromBody] StartCallRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.PhoneNumber))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Start call failed",
                Detail = "Phone number is required.",
                Status = StatusCodes.Status400BadRequest,
            });
        }

        try
        {
            var externalCallId = string.IsNullOrWhiteSpace(request.ExternalCallId)
                ? $"desktop-{Guid.NewGuid():N}"
                : request.ExternalCallId.Trim();

            await agentActivityService.RecordCallStartedAsync(
                User.GetRequiredTenantId(),
                User.GetRequiredUserId(),
                request.LeadId,
                externalCallId,
                cancellationToken);

            return Ok(new StartCallResponse(
                true,
                externalCallId,
                "Zoom desktop call logging started."));
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Start call failed",
                Detail = exception.Message,
                Status = StatusCodes.Status400BadRequest,
            });
        }
    }

    [HttpPost("call/hangup")]
    public async Task<IActionResult> HangUp([FromBody] HangUpCallRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ExternalCallId))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Hang up failed",
                Detail = "External call id is required.",
                Status = StatusCodes.Status400BadRequest,
            });
        }

        try
        {
            await agentActivityService.RecordCallEndedAsync(
                User.GetRequiredTenantId(),
                User.GetRequiredUserId(),
                request.ExternalCallId.Trim(),
                request.WasAnswered,
                request.RequeueLead,
                request.OutcomeLabel,
                cancellationToken);

            return NoContent();
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Hang up failed",
                Detail = exception.Message,
                Status = StatusCodes.Status400BadRequest,
            });
        }
    }
}
