using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NewDialer.Api.Contracts;
using NewDialer.Api.Security;
using NewDialer.Application.Abstractions;
using NewDialer.Application.Models;
using NewDialer.Contracts.Leads;

namespace NewDialer.Api.Controllers;

[ApiController]
[Route("api/leads")]
[Authorize]
public sealed class LeadsController(ILeadManagementService leadManagementService) : ControllerBase
{
    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<IReadOnlyList<LeadDto>>> GetLeads([FromQuery] Guid? assignedAgentId, CancellationToken cancellationToken)
    {
        var tenantId = User.GetRequiredTenantId();
        var leads = await leadManagementService.GetTenantLeadsAsync(tenantId, assignedAgentId, cancellationToken);
        return Ok(leads);
    }

    [HttpGet("assigned")]
    [Authorize(Roles = "Admin,Agent")]
    public async Task<ActionResult<IReadOnlyList<LeadDto>>> GetAssignedLeads([FromQuery] Guid? agentId, CancellationToken cancellationToken)
    {
        var tenantId = User.GetRequiredTenantId();
        var effectiveAgentId = User.IsInRole("Admin")
            ? agentId ?? User.GetRequiredUserId()
            : User.GetRequiredUserId();

        var leads = await leadManagementService.GetAssignedLeadsAsync(tenantId, effectiveAgentId, cancellationToken);
        return Ok(leads);
    }

    [HttpGet("agents")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<IReadOnlyList<AgentAssignmentOptionDto>>> GetAgentOptions(CancellationToken cancellationToken)
    {
        var tenantId = User.GetRequiredTenantId();
        var agents = await leadManagementService.GetAgentOptionsAsync(tenantId, cancellationToken);
        return Ok(agents);
    }

    [HttpGet("import-batches")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<IReadOnlyList<LeadImportBatchDto>>> GetImportBatches(CancellationToken cancellationToken)
    {
        var tenantId = User.GetRequiredTenantId();
        var batches = await leadManagementService.GetImportBatchesAsync(tenantId, cancellationToken);
        return Ok(batches);
    }

    [HttpPost("import")]
    [Authorize(Roles = "Admin")]
    [RequestSizeLimit(25_000_000)]
    public async Task<ActionResult<LeadImportResultDto>> ImportLeads([FromForm] LeadImportForm form, CancellationToken cancellationToken)
    {
        if (form.File is null || form.File.Length == 0)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Import failed",
                Detail = "Upload a non-empty Excel file.",
                Status = StatusCodes.Status400BadRequest,
            });
        }

        var tenantId = User.GetRequiredTenantId();
        var userId = User.GetRequiredUserId();

        try
        {
            await using var stream = form.File.OpenReadStream();
            var result = await leadManagementService.ImportLeadsAsync(
                new LeadImportCommand(
                    TenantId: tenantId,
                    UploadedByUserId: userId,
                    FileName: form.File.FileName,
                    Notes: form.Notes,
                    DefaultAgentId: form.DefaultAgentId),
                stream,
                cancellationToken);

            return Ok(result);
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Import failed",
                Detail = exception.Message,
                Status = StatusCodes.Status400BadRequest,
            });
        }
    }

    [HttpPost("assignments")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> AssignLeads([FromBody] LeadAssignmentRequest request, CancellationToken cancellationToken)
    {
        try
        {
            await leadManagementService.AssignLeadsAsync(
                new AssignLeadsCommand(
                    TenantId: User.GetRequiredTenantId(),
                    AgentId: request.AgentId,
                    LeadIds: request.LeadIds),
                cancellationToken);

            return NoContent();
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Lead assignment failed",
                Detail = exception.Message,
                Status = StatusCodes.Status400BadRequest,
            });
        }
    }
}
