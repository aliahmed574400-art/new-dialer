using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NewDialer.Application.Abstractions;
using NewDialer.Contracts.Platform;

namespace NewDialer.Api.Controllers;

[ApiController]
[Route("api/platform")]
[Authorize(Roles = "Developer")]
public sealed class PlatformController(IPlatformOverviewService platformOverviewService) : ControllerBase
{
    [HttpGet("overview")]
    public async Task<ActionResult<IReadOnlyList<TenantOverviewDto>>> GetOverview(CancellationToken cancellationToken)
    {
        var tenants = await platformOverviewService.GetTenantOverviewAsync(cancellationToken);
        return Ok(tenants);
    }
}
