namespace NewDialer.Contracts.Auth;

public sealed record LoginRequest(
    string UsernameOrEmail,
    string Password,
    string? WorkspaceKey);
