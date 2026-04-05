using NewDialer.Domain.Common;
using NewDialer.Domain.Enums;

namespace NewDialer.Domain.Entities;

public sealed class CallAttempt : TenantEntity
{
    public Guid LeadId { get; set; }

    public Lead? Lead { get; set; }

    public Guid AgentId { get; set; }

    public ApplicationUser? Agent { get; set; }

    public string ExternalCallId { get; set; } = string.Empty;

    public DateTimeOffset StartedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? EndedAtUtc { get; set; }

    public int DurationSeconds { get; set; }

    public CallDisposition Disposition { get; set; } = CallDisposition.None;

    public bool WasAnswered { get; set; }

    public string Notes { get; set; } = string.Empty;
}
