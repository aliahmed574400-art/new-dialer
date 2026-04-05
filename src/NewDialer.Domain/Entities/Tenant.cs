using NewDialer.Domain.Common;

namespace NewDialer.Domain.Entities;

public sealed class Tenant : AuditableEntity
{
    public string WorkspaceKey { get; set; } = string.Empty;

    public string CompanyName { get; set; } = string.Empty;

    public string OwnerName { get; set; } = string.Empty;

    public string OwnerEmail { get; set; } = string.Empty;

    public string OwnerPhoneNumber { get; set; } = string.Empty;

    public string TimeZoneId { get; set; } = "UTC";

    public bool IsActive { get; set; } = true;

    public ICollection<ApplicationUser> Users { get; set; } = [];

    public ICollection<Lead> Leads { get; set; } = [];

    public ICollection<LeadImportBatch> LeadImportBatches { get; set; } = [];

    public ICollection<ScheduledCall> ScheduledCalls { get; set; } = [];

    public ICollection<TenantSubscription> Subscriptions { get; set; } = [];
}
