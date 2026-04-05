namespace NewDialer.Infrastructure.Auth;

public sealed class SubscriptionOptions
{
    public const string SectionName = "Subscription";

    public int TrialDays { get; set; } = 15;
}
