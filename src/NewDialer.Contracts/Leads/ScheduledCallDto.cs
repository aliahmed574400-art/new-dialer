using NewDialer.Domain.Enums;

namespace NewDialer.Contracts.Leads;

public sealed record ScheduledCallDto(
    Guid Id,
    Guid LeadId,
    Guid AgentId,
    DateTimeOffset ScheduledForUtc,
    string TimeZoneId,
    string Notes,
    ScheduleStatus Status);
