namespace NewDialer.Api;

public sealed record PlatformStatusResponse(
    string Name,
    string Version,
    DateTimeOffset UtcTime,
    IReadOnlyList<string> Modules,
    string SampleSubscriptionMessage);
