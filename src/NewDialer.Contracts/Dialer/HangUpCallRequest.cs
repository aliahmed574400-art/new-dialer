namespace NewDialer.Contracts.Dialer;

public sealed record HangUpCallRequest(
    string ExternalCallId,
    bool WasAnswered = false,
    bool RequeueLead = false,
    string? OutcomeLabel = null);
