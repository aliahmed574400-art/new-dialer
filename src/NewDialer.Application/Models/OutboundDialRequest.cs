namespace NewDialer.Application.Models;

public sealed record OutboundDialRequest(
    Guid TenantId,
    Guid AgentId,
    Guid LeadId,
    string PhoneNumber,
    string DisplayName);
