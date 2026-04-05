using NewDialer.Domain.Enums;

namespace NewDialer.Application.Models;

public sealed record SubscriptionAccessDecision(
    SubscriptionStatus EffectiveStatus,
    bool CanUseDialer,
    bool CanViewData,
    int DaysRemaining,
    string Reason);
