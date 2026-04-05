namespace NewDialer.Contracts.Leads;

public sealed record LeadImportBatchDto(
    Guid Id,
    string FileName,
    int TotalRows,
    int ImportedRows,
    string Notes,
    DateTimeOffset ImportedAtUtc,
    Guid UploadedByUserId);
