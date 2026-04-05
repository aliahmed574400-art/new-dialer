using NewDialer.Contracts.Analytics;
using NewDialer.Domain.Enums;

namespace NewDialer.Application.Abstractions;

public interface IAgentActivityService
{
    Task RecordSignInAsync(Guid tenantId, Guid userId, UserRole role, CancellationToken cancellationToken);

    Task RecordSignOutAsync(Guid tenantId, Guid userId, UserRole role, CancellationToken cancellationToken);

    Task RecordCallStartedAsync(Guid tenantId, Guid agentId, Guid leadId, string externalCallId, CancellationToken cancellationToken);

    Task RecordCallEndedAsync(
        Guid tenantId,
        Guid agentId,
        string externalCallId,
        bool wasAnswered,
        bool requeueLead,
        string? outcomeLabel,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<AgentPerformanceDto>> GetDailyPerformanceAsync(Guid tenantId, CancellationToken cancellationToken);
}
