namespace NewDialer.Application.Models;

public sealed record ZoomCallStartResult(
    bool Succeeded,
    string ExternalCallId,
    string Message);
