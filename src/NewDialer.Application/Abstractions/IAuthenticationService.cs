using NewDialer.Contracts.Auth;

namespace NewDialer.Application.Abstractions;

public interface IAuthenticationService
{
    Task<SessionDto> RegisterAdminAsync(AdminSignupRequest request, CancellationToken cancellationToken);

    Task<SessionDto?> LoginAsync(LoginRequest request, CancellationToken cancellationToken);
}
