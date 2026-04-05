using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NewDialer.Api.Security;
using NewDialer.Application.Abstractions;
using NewDialer.Contracts.Analytics;

namespace NewDialer.Api.Controllers;

[ApiController]
[Route("api/agents")]
[Authorize(Roles = "Admin")]
public sealed class AgentsController(IAgentActivityService agentActivityService) : ControllerBase
{
    [HttpGet("performance")]
    public async Task<ActionResult<IReadOnlyList<AgentPerformanceDto>>> GetPerformance(CancellationToken cancellationToken)
    {
        var performance = await agentActivityService.GetDailyPerformanceAsync(User.GetRequiredTenantId(), cancellationToken);
        return Ok(performance);
    }
}
