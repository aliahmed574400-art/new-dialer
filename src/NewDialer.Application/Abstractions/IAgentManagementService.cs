using NewDialer.Contracts.Agents;

namespace NewDialer.Application.Abstractions;

public interface IAgentManagementService
{
    Task<IReadOnlyList<AgentAdminDto>> GetAgentsAsync(Guid tenantId, CancellationToken cancellationToken);

    Task<AgentAdminDto> CreateAgentAsync(Guid tenantId, CreateAgentRequest request, CancellationToken cancellationToken);

    Task<AgentAdminDto> UpdateAgentAsync(Guid tenantId, Guid agentId, UpdateAgentRequest request, CancellationToken cancellationToken);

    Task DeleteAgentAsync(Guid tenantId, Guid agentId, CancellationToken cancellationToken);
}
