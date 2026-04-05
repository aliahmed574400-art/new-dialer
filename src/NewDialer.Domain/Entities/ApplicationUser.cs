using NewDialer.Domain.Common;
using NewDialer.Domain.Enums;

namespace NewDialer.Domain.Entities;

public sealed class ApplicationUser : TenantEntity
{
    public string Username { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public string PhoneNumber { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public UserRole Role { get; set; } = UserRole.Agent;

    public bool IsEnabled { get; set; } = true;

    public bool CanCopyLeadData { get; set; }

    public DateTimeOffset? LastLoginAtUtc { get; set; }

    public ICollection<Lead> AssignedLeads { get; set; } = [];

    public ICollection<ScheduledCall> ScheduledCalls { get; set; } = [];

    public ICollection<CallAttempt> CallAttempts { get; set; } = [];

    public ICollection<WorkSession> WorkSessions { get; set; } = [];

    public ICollection<DialerRun> DialerRuns { get; set; } = [];
}
