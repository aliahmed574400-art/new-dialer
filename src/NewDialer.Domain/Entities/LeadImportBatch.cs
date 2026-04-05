using NewDialer.Domain.Common;

namespace NewDialer.Domain.Entities;

public sealed class LeadImportBatch : TenantEntity
{
    public Guid UploadedByUserId { get; set; }

    public ApplicationUser? UploadedByUser { get; set; }

    public string FileName { get; set; } = string.Empty;

    public int TotalRows { get; set; }

    public int ImportedRows { get; set; }

    public string Notes { get; set; } = string.Empty;

    public DateTimeOffset ImportedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<Lead> Leads { get; set; } = [];
}
