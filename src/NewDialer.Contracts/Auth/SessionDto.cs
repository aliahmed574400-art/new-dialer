using NewDialer.Domain.Enums;

namespace NewDialer.Contracts.Auth;

public sealed record SessionDto(
    Guid UserId,
    Guid TenantId,
    string FullName,
    string CompanyName,
    string WorkspaceKey,
    string AccessToken,
    UserRole Role,
    string TimeZoneId,
    SubscriptionStatus SubscriptionStatus,
    bool CanUseDialer,
    bool CanViewData,
    string SubscriptionMessage);
