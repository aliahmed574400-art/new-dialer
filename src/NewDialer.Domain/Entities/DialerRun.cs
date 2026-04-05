using NewDialer.Domain.Common;
using NewDialer.Domain.Enums;

namespace NewDialer.Domain.Entities;

public sealed class DialerRun : TenantEntity
{
    public Guid AgentId { get; set; }

    public ApplicationUser? Agent { get; set; }

    public DialerRunStatus Status { get; set; } = DialerRunStatus.Idle;

    public DateTimeOffset StartedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? PausedAtUtc { get; set; }

    public DateTimeOffset? StoppedAtUtc { get; set; }

    public Guid? CurrentLeadId { get; set; }

    public int TotalQueued { get; set; }

    public int TotalCompleted { get; set; }

    public int TotalFailed { get; set; }
}
