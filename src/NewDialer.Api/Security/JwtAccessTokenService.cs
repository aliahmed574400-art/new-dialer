using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using NewDialer.Application.Abstractions;
using NewDialer.Application.Models;

namespace NewDialer.Api.Security;

public sealed class JwtAccessTokenService(IOptions<JwtOptions> jwtOptions) : IAccessTokenService
{
    public string CreateAccessToken(AccessTokenRequest request)
    {
        var options = jwtOptions.Value;
        var nowUtc = DateTime.UtcNow;
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, request.UserId.ToString()),
            new(ClaimTypes.NameIdentifier, request.UserId.ToString()),
            new(ClaimTypes.Name, request.FullName),
            new(ClaimTypes.Email, request.Email),
            new(ClaimTypes.Role, request.Role.ToString()),
            new("tenant_id", request.TenantId.ToString()),
            new("workspace_key", request.WorkspaceKey),
        };

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: options.Issuer,
            audience: options.Audience,
            claims: claims,
            notBefore: nowUtc,
            expires: nowUtc.AddMinutes(options.AccessTokenMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
