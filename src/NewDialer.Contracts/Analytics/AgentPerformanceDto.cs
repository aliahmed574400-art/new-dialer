namespace NewDialer.Contracts.Analytics;

public sealed record AgentPerformanceDto(
    Guid AgentId,
    string AgentName,
    int CallsToday,
    int TalkSeconds,
    DateTimeOffset? CheckInAtUtc,
    DateTimeOffset? CheckOutAtUtc);
