namespace NewDialer.Contracts.Agents;

public sealed record CreateAgentRequest(
    string FullName,
    string Email,
    string Password);
