namespace NewDialer.Contracts.Leads;

public sealed record LeadImportResultDto(
    Guid BatchId,
    string FileName,
    int TotalRows,
    int ImportedRows,
    int SkippedRows,
    Guid? DefaultAgentId,
    string Message);
