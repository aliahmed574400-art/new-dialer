namespace NewDialer.Application.Models;

public sealed record AssignLeadsCommand(
    Guid TenantId,
    Guid AgentId,
    IReadOnlyCollection<Guid> LeadIds);
