namespace NewDialer.Contracts.Dashboard;

public sealed record AgentMetricsDto(
    Guid AgentId,
    string AgentName,
    int CallsToday,
    int TalkSecondsToday,
    DateTimeOffset? CheckInAtUtc,
    DateTimeOffset? CheckOutAtUtc);
