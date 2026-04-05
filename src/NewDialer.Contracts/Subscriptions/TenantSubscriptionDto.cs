using NewDialer.Domain.Enums;

namespace NewDialer.Contracts.Subscriptions;

public sealed record TenantSubscriptionDto(
    Guid TenantId,
    string CompanyName,
    string PlanName,
    SubscriptionStatus Status,
    DateTimeOffset TrialEndsAtUtc,
    DateTimeOffset? CurrentPeriodEndsAtUtc,
    bool IsManualActivation);
