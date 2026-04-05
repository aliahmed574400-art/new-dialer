namespace NewDialer.Desktop.Models;

public sealed class ScheduledCallEntry
{
    public Guid Id { get; init; }

    public Guid LeadId { get; init; }

    public Guid AgentId { get; init; }

    public required string LeadName { get; init; }

    public required string AgentName { get; init; }

    public required string PhoneNumber { get; init; }

    public required string TimeZoneId { get; init; }

    public required string Notes { get; init; }

    public required DateTimeOffset ScheduledForUtc { get; init; }

    public string Status { get; init; } = "Pending";

    public string LocalDisplay => ScheduledForUtc.ToLocalTime().ToString("dd MMM yyyy HH:mm");
}
