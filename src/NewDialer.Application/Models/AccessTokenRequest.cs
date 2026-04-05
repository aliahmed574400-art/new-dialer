using NewDialer.Domain.Enums;

namespace NewDialer.Application.Models;

public sealed record AccessTokenRequest(
    Guid UserId,
    Guid TenantId,
    string WorkspaceKey,
    string Email,
    string FullName,
    UserRole Role);
