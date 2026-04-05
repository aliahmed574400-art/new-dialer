using NewDialer.Application;
using NewDialer.Domain.Entities;
using NewDialer.Domain.Enums;

namespace NewDialer.Domain.Tests;

public class SubscriptionAccessEvaluatorTests
{
    private readonly SubscriptionAccessEvaluator _sut = new();

    [Fact]
    public void Trial_before_end_allows_dialing()
    {
        var subscription = new TenantSubscription
        {
            Status = SubscriptionStatus.Trial,
            TrialStartedAtUtc = new DateTimeOffset(2026, 4, 1, 0, 0, 0, TimeSpan.Zero),
            TrialEndsAtUtc = new DateTimeOffset(2026, 4, 15, 0, 0, 0, TimeSpan.Zero),
        };

        var result = _sut.Evaluate(subscription, new DateTimeOffset(2026, 4, 4, 0, 0, 0, TimeSpan.Zero));

        Assert.True(result.CanUseDialer);
        Assert.True(result.CanViewData);
        Assert.Equal(SubscriptionStatus.Trial, result.EffectiveStatus);
    }

    [Fact]
    public void Trial_after_end_becomes_read_only()
    {
        var subscription = new TenantSubscription
        {
            Status = SubscriptionStatus.Trial,
            TrialStartedAtUtc = new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero),
            TrialEndsAtUtc = new DateTimeOffset(2026, 3, 16, 0, 0, 0, TimeSpan.Zero),
        };

        var result = _sut.Evaluate(subscription, new DateTimeOffset(2026, 4, 4, 0, 0, 0, TimeSpan.Zero));

        Assert.False(result.CanUseDialer);
        Assert.True(result.CanViewData);
        Assert.Equal(SubscriptionStatus.Expired, result.EffectiveStatus);
    }
}
