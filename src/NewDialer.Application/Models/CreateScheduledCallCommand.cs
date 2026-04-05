namespace NewDialer.Application.Models;

public sealed record CreateScheduledCallCommand(
    Guid TenantId,
    Guid LeadId,
    Guid AgentId,
    DateTimeOffset ScheduledForUtc,
    string TimeZoneId,
    string Notes,
    bool RequireAssignedLeadOwnership);
