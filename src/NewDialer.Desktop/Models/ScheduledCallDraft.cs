namespace NewDialer.Desktop.Models;

public sealed record ScheduledCallDraft(
    DateTimeOffset ScheduledForUtc,
    string TimeZoneId,
    string Notes);
