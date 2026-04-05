using NewDialer.Application.Abstractions;
using NewDialer.Application.Models;
using NewDialer.Domain.Entities;
using NewDialer.Domain.Enums;

namespace NewDialer.Application;

public sealed class SubscriptionAccessEvaluator : ISubscriptionAccessEvaluator
{
    public SubscriptionAccessDecision Evaluate(TenantSubscription subscription, DateTimeOffset nowUtc)
    {
        if (subscription.Status == SubscriptionStatus.Trial)
        {
            var trialDaysRemaining = Math.Max(0, (subscription.TrialEndsAtUtc - nowUtc).Days);
            var trialActive = nowUtc <= subscription.TrialEndsAtUtc;

            return new SubscriptionAccessDecision(
                trialActive ? SubscriptionStatus.Trial : SubscriptionStatus.Expired,
                CanUseDialer: trialActive,
                CanViewData: true,
                DaysRemaining: trialDaysRemaining,
                Reason: trialActive
                    ? $"Trial active for {trialDaysRemaining} more day(s)."
                    : "Trial finished. Dialing is disabled until a subscription is activated.");
        }

        if (subscription.Status == SubscriptionStatus.Active)
        {
            var currentPeriodOpen = !subscription.CurrentPeriodEndsAtUtc.HasValue || nowUtc <= subscription.CurrentPeriodEndsAtUtc.Value;
            var activeDaysRemaining = subscription.CurrentPeriodEndsAtUtc.HasValue
                ? Math.Max(0, (subscription.CurrentPeriodEndsAtUtc.Value - nowUtc).Days)
                : int.MaxValue;

            return new SubscriptionAccessDecision(
                currentPeriodOpen ? SubscriptionStatus.Active : SubscriptionStatus.Expired,
                CanUseDialer: currentPeriodOpen,
                CanViewData: true,
                DaysRemaining: activeDaysRemaining,
                Reason: currentPeriodOpen
                    ? "Subscription is active."
                    : "Subscription period ended. Dialing is disabled until renewal.");
        }

        return new SubscriptionAccessDecision(
            subscription.Status,
            CanUseDialer: false,
            CanViewData: true,
            DaysRemaining: 0,
            Reason: subscription.Status switch
            {
                SubscriptionStatus.PastDue => "Subscription payment is due. Dialing is paused until resolved.",
                SubscriptionStatus.Suspended => "Subscription is suspended. Contact support or your developer team.",
                SubscriptionStatus.Expired => "Subscription expired. Dialing is disabled until renewal.",
                _ => "Subscription does not allow dialing.",
            });
    }
}
