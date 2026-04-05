using NewDialer.Application.Models;
using NewDialer.Contracts.Leads;

namespace NewDialer.Application.Abstractions;

public interface ILeadManagementService
{
    Task<LeadImportResultDto> ImportLeadsAsync(LeadImportCommand command, Stream fileStream, CancellationToken cancellationToken);

    Task AssignLeadsAsync(AssignLeadsCommand command, CancellationToken cancellationToken);

    Task<IReadOnlyList<LeadDto>> GetTenantLeadsAsync(Guid tenantId, Guid? assignedAgentId, CancellationToken cancellationToken);

    Task<IReadOnlyList<LeadDto>> GetAssignedLeadsAsync(Guid tenantId, Guid agentId, CancellationToken cancellationToken);

    Task<IReadOnlyList<LeadImportBatchDto>> GetImportBatchesAsync(Guid tenantId, CancellationToken cancellationToken);

    Task<IReadOnlyList<AgentAssignmentOptionDto>> GetAgentOptionsAsync(Guid tenantId, CancellationToken cancellationToken);

    Task<IReadOnlyList<ScheduledCallListItemDto>> GetScheduledCallsAsync(Guid tenantId, Guid? agentId, CancellationToken cancellationToken);

    Task<ScheduledCallListItemDto> CreateScheduledCallAsync(CreateScheduledCallCommand command, CancellationToken cancellationToken);

    Task<ScheduleExportDocument> ExportSchedulesAsync(ScheduleExportRequest request, CancellationToken cancellationToken);
}
