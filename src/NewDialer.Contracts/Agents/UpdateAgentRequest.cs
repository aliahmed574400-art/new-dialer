namespace NewDialer.Contracts.Agents;

public sealed record UpdateAgentRequest(
    string FullName,
    string Email,
    string? Password);
