using NewDialer.Domain.Common;
using NewDialer.Domain.Enums;

namespace NewDialer.Domain.Entities;

public sealed class ScheduledCall : TenantEntity
{
    public Guid LeadId { get; set; }

    public Lead? Lead { get; set; }

    public Guid AgentId { get; set; }

    public ApplicationUser? Agent { get; set; }

    public DateTimeOffset ScheduledForUtc { get; set; }

    public string TimeZoneId { get; set; } = "UTC";

    public string Notes { get; set; } = string.Empty;

    public ScheduleStatus Status { get; set; } = ScheduleStatus.Pending;

    public DateTimeOffset? CompletedAtUtc { get; set; }
}
