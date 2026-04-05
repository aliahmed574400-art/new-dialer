using System.Security.Claims;
using NewDialer.Domain.Enums;

namespace NewDialer.Api.Security;

public static class ClaimsPrincipalExtensions
{
    public static Guid GetRequiredTenantId(this ClaimsPrincipal user)
    {
        return GetRequiredGuid(user, "tenant_id");
    }

    public static Guid GetRequiredUserId(this ClaimsPrincipal user)
    {
        return GetRequiredGuid(user, ClaimTypes.NameIdentifier);
    }

    public static UserRole GetRequiredRole(this ClaimsPrincipal user)
    {
        var rawValue = user.FindFirstValue(ClaimTypes.Role);
        if (!Enum.TryParse<UserRole>(rawValue, ignoreCase: true, out var role))
        {
            throw new InvalidOperationException("The required role claim is missing.");
        }

        return role;
    }

    private static Guid GetRequiredGuid(ClaimsPrincipal user, string claimType)
    {
        var rawValue = user.FindFirstValue(claimType);
        if (!Guid.TryParse(rawValue, out var value))
        {
            throw new InvalidOperationException($"The required claim '{claimType}' is missing.");
        }

        return value;
    }
}
