using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NewDialer.Api.Security;
using NewDialer.Application.Abstractions;
using NewDialer.Application.Models;
using NewDialer.Contracts.Leads;

namespace NewDialer.Api.Controllers;

[ApiController]
[Route("api/schedules")]
[Authorize(Roles = "Admin,Agent")]
public sealed class SchedulesController(ILeadManagementService leadManagementService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ScheduledCallListItemDto>>> GetScheduledCalls(
        [FromQuery] Guid? agentId,
        CancellationToken cancellationToken)
    {
        var effectiveAgentId = User.IsInRole("Agent")
            ? User.GetRequiredUserId()
            : agentId;

        var schedules = await leadManagementService.GetScheduledCallsAsync(
            User.GetRequiredTenantId(),
            effectiveAgentId,
            cancellationToken);

        return Ok(schedules);
    }

    [HttpPost]
    public async Task<ActionResult<ScheduledCallListItemDto>> Create(
        [FromBody] CreateScheduledCallRequest request,
        CancellationToken cancellationToken)
    {
        if (request.LeadId == Guid.Empty)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Schedule failed",
                Detail = "Lead id is required.",
                Status = StatusCodes.Status400BadRequest,
            });
        }

        try
        {
            var schedule = await leadManagementService.CreateScheduledCallAsync(
                new CreateScheduledCallCommand(
                    TenantId: User.GetRequiredTenantId(),
                    LeadId: request.LeadId,
                    AgentId: User.GetRequiredUserId(),
                    ScheduledForUtc: request.ScheduledForUtc,
                    TimeZoneId: request.TimeZoneId,
                    Notes: request.Notes,
                    RequireAssignedLeadOwnership: User.IsInRole("Agent")),
                cancellationToken);

            return Ok(schedule);
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Schedule failed",
                Detail = exception.Message,
                Status = StatusCodes.Status400BadRequest,
            });
        }
    }

    [HttpGet("export")]
    public async Task<IActionResult> Export(
        [FromQuery] DateTimeOffset? fromUtc,
        [FromQuery] DateTimeOffset? toUtc,
        [FromQuery] Guid? agentId,
        CancellationToken cancellationToken)
    {
        var effectiveAgentId = User.IsInRole("Agent")
            ? User.GetRequiredUserId()
            : agentId;

        var export = await leadManagementService.ExportSchedulesAsync(
            new ScheduleExportRequest(
                TenantId: User.GetRequiredTenantId(),
                AgentId: effectiveAgentId,
                FromUtc: fromUtc,
                ToUtc: toUtc),
            cancellationToken);

        return File(export.Content, export.ContentType, export.FileName);
    }
}
