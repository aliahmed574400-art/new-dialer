namespace NewDialer.Application.Models;

public sealed record ScheduleExportRow(
    string LeadName,
    string PhoneNumber,
    string AgentName,
    string TimeZoneId,
    DateTimeOffset ScheduledForUtc,
    string Notes);
