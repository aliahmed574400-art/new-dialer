namespace NewDialer.Contracts.Dialer;

public sealed record StartCallRequest(
    Guid LeadId,
    string PhoneNumber,
    string DisplayName,
    string? ExternalCallId = null);
