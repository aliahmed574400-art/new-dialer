namespace NewDialer.Domain.Common;

public abstract class TenantEntity : AuditableEntity
{
    public Guid TenantId { get; set; }

    public Entities.Tenant? Tenant { get; set; }
}
