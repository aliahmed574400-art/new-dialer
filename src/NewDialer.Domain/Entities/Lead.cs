using NewDialer.Domain.Common;
using NewDialer.Domain.Enums;

namespace NewDialer.Domain.Entities;

public sealed class Lead : TenantEntity
{
    public Guid? ImportBatchId { get; set; }

    public LeadImportBatch? ImportBatch { get; set; }

    public Guid? AssignedAgentId { get; set; }

    public ApplicationUser? AssignedAgent { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string PhoneNumber { get; set; } = string.Empty;

    public string Website { get; set; } = string.Empty;

    public string Service { get; set; } = string.Empty;

    public string Budget { get; set; } = string.Empty;

    public LeadStatus Status { get; set; } = LeadStatus.New;

    public string LastOutcome { get; set; } = string.Empty;

    public bool IsDoNotCall { get; set; }

    public ICollection<ScheduledCall> ScheduledCalls { get; set; } = [];

    public ICollection<CallAttempt> CallAttempts { get; set; } = [];
}
