namespace NewDialer.Contracts.Agents;

public sealed record AgentAdminDto(
    Guid AgentId,
    string FullName,
    string Email,
    string Username,
    bool IsEnabled,
    int CallsToday,
    int ScheduledCalls,
    int TalkSeconds,
    DateTimeOffset? CheckInAtUtc,
    DateTimeOffset? CheckOutAtUtc);
