namespace NewDialer.Application.Models;

public sealed record LeadSpreadsheetReadResult(
    IReadOnlyList<ImportedLeadDraft> Leads,
    int TotalRows,
    int SkippedRows);
