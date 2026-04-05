namespace NewDialer.Contracts.Leads;

public sealed record LeadAssignmentRequest(
    Guid AgentId,
    IReadOnlyCollection<Guid> LeadIds);
