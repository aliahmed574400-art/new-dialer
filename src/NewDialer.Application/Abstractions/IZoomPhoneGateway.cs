using NewDialer.Application.Models;

namespace NewDialer.Application.Abstractions;

public interface IZoomPhoneGateway
{
    Task<ZoomCallStartResult> StartOutboundCallAsync(OutboundDialRequest request, CancellationToken cancellationToken);

    Task HangUpAsync(string externalCallId, CancellationToken cancellationToken);
}
