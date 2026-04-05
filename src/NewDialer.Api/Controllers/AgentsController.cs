using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NewDialer.Api.Security;
using NewDialer.Application.Abstractions;
using NewDialer.Contracts.Agents;
using NewDialer.Contracts.Analytics;

namespace NewDialer.Api.Controllers;

[ApiController]
[Route("api/agents")]
[Authorize(Roles = "Admin")]
public sealed class AgentsController(
    IAgentActivityService agentActivityService,
    IAgentManagementService agentManagementService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AgentAdminDto>>> GetAgents(CancellationToken cancellationToken)
    {
        var agents = await agentManagementService.GetAgentsAsync(User.GetRequiredTenantId(), cancellationToken);
        return Ok(agents);
    }

    [HttpGet("performance")]
    public async Task<ActionResult<IReadOnlyList<AgentPerformanceDto>>> GetPerformance(CancellationToken cancellationToken)
    {
        var performance = await agentActivityService.GetDailyPerformanceAsync(User.GetRequiredTenantId(), cancellationToken);
        return Ok(performance);
    }

    [HttpPost]
    public async Task<ActionResult<AgentAdminDto>> CreateAgent([FromBody] CreateAgentRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var createdAgent = await agentManagementService.CreateAgentAsync(User.GetRequiredTenantId(), request, cancellationToken);
            return Ok(createdAgent);
        }
        catch (InvalidOperationException exception)
        {
            return exception.Message.Contains("already exists", StringComparison.OrdinalIgnoreCase)
                ? Conflict(CreateProblemDetails("Create agent failed", exception.Message, StatusCodes.Status409Conflict))
                : BadRequest(CreateProblemDetails("Create agent failed", exception.Message, StatusCodes.Status400BadRequest));
        }
    }

    [HttpPut("{agentId:guid}")]
    public async Task<ActionResult<AgentAdminDto>> UpdateAgent(Guid agentId, [FromBody] UpdateAgentRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var updatedAgent = await agentManagementService.UpdateAgentAsync(User.GetRequiredTenantId(), agentId, request, cancellationToken);
            return Ok(updatedAgent);
        }
        catch (InvalidOperationException exception)
        {
            return exception.Message.Contains("not found", StringComparison.OrdinalIgnoreCase)
                ? NotFound(CreateProblemDetails("Update agent failed", exception.Message, StatusCodes.Status404NotFound))
                : exception.Message.Contains("already exists", StringComparison.OrdinalIgnoreCase)
                    ? Conflict(CreateProblemDetails("Update agent failed", exception.Message, StatusCodes.Status409Conflict))
                    : BadRequest(CreateProblemDetails("Update agent failed", exception.Message, StatusCodes.Status400BadRequest));
        }
    }

    [HttpDelete("{agentId:guid}")]
    public async Task<IActionResult> DeleteAgent(Guid agentId, CancellationToken cancellationToken)
    {
        try
        {
            await agentManagementService.DeleteAgentAsync(User.GetRequiredTenantId(), agentId, cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException exception)
        {
            return exception.Message.Contains("not found", StringComparison.OrdinalIgnoreCase)
                ? NotFound(CreateProblemDetails("Delete agent failed", exception.Message, StatusCodes.Status404NotFound))
                : BadRequest(CreateProblemDetails("Delete agent failed", exception.Message, StatusCodes.Status400BadRequest));
        }
    }

    private static ProblemDetails CreateProblemDetails(string title, string detail, int statusCode)
    {
        return new ProblemDetails
        {
            Title = title,
            Detail = detail,
            Status = statusCode,
        };
    }
}
