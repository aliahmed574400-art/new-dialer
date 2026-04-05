using NewDialer.Domain.Enums;

namespace NewDialer.Contracts.Leads;

public sealed record ScheduledCallListItemDto(
    Guid Id,
    Guid LeadId,
    string LeadName,
    string PhoneNumber,
    Guid AgentId,
    string AgentName,
    DateTimeOffset ScheduledForUtc,
    string TimeZoneId,
    string Notes,
    ScheduleStatus Status);
