using NewDialer.Domain.Common;
using NewDialer.Domain.Enums;

namespace NewDialer.Domain.Entities;

public sealed class TenantSubscription : TenantEntity
{
    public string PlanName { get; set; } = "Trial";

    public SubscriptionStatus Status { get; set; } = SubscriptionStatus.Trial;

    public DateTimeOffset TrialStartedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset TrialEndsAtUtc { get; set; } = DateTimeOffset.UtcNow.AddDays(15);

    public DateTimeOffset? CurrentPeriodStartUtc { get; set; }

    public DateTimeOffset? CurrentPeriodEndsAtUtc { get; set; }

    public bool IsManualActivation { get; set; }

    public Guid? ActivatedByDeveloperUserId { get; set; }

    public string PaymentReference { get; set; } = string.Empty;

    public string Notes { get; set; } = string.Empty;
}
