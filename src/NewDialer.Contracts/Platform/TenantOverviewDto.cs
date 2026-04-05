using NewDialer.Domain.Enums;

namespace NewDialer.Contracts.Platform;

public sealed record TenantOverviewDto(
    Guid TenantId,
    string CompanyName,
    string AdminEmail,
    string PlanName,
    SubscriptionStatus Status,
    DateTimeOffset? TrialEndsAtUtc,
    DateTimeOffset? CurrentPeriodEndsAtUtc);
