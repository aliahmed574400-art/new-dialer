namespace NewDialer.Contracts.Leads;

public sealed record AgentAssignmentOptionDto(
    Guid AgentId,
    string FullName,
    string Username,
    bool IsEnabled);
