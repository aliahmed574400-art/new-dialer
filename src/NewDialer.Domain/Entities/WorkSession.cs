using NewDialer.Domain.Common;

namespace NewDialer.Domain.Entities;

public sealed class WorkSession : TenantEntity
{
    public Guid AgentId { get; set; }

    public ApplicationUser? Agent { get; set; }

    public DateTimeOffset CheckInAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? CheckOutAtUtc { get; set; }

    public int TotalCalls { get; set; }

    public int TotalTalkSeconds { get; set; }

    public int TotalPausedSeconds { get; set; }
}
