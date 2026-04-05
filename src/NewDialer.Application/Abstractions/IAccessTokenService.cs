using NewDialer.Application.Models;

namespace NewDialer.Application.Abstractions;

public interface IAccessTokenService
{
    string CreateAccessToken(AccessTokenRequest request);
}
