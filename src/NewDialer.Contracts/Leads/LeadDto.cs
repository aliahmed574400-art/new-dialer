using NewDialer.Domain.Enums;

namespace NewDialer.Contracts.Leads;

public sealed record LeadDto(
    Guid Id,
    string Name,
    string Email,
    string PhoneNumber,
    string Website,
    string Service,
    string Budget,
    LeadStatus Status,
    Guid? AssignedAgentId,
    string? AssignedAgentName,
    Guid? ImportBatchId);
