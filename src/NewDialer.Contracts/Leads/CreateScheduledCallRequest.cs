namespace NewDialer.Contracts.Leads;

public sealed record CreateScheduledCallRequest(
    Guid LeadId,
    DateTimeOffset ScheduledForUtc,
    string TimeZoneId,
    string Notes);
