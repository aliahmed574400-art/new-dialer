namespace NewDialer.Domain.Enums;

public enum LeadStatus
{
    New = 1,
    Queued = 2,
    Dialing = 3,
    Completed = 4,
    FollowUpScheduled = 5,
    DoNotCall = 6,
    Failed = 7,
}
