namespace NewDialer.Application.Models;

public sealed record ImportedLeadDraft(
    int RowNumber,
    string Name,
    string Email,
    string PhoneNumber,
    string Website,
    string Service,
    string Budget);
