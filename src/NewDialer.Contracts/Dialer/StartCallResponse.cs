namespace NewDialer.Contracts.Dialer;

public sealed record StartCallResponse(
    bool Succeeded,
    string ExternalCallId,
    string Message);
