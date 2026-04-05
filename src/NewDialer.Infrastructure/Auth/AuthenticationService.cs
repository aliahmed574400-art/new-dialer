using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NewDialer.Application.Abstractions;
using NewDialer.Application.Models;
using NewDialer.Contracts.Auth;
using NewDialer.Domain.Entities;
using NewDialer.Domain.Enums;
using NewDialer.Infrastructure.Persistence;

namespace NewDialer.Infrastructure.Auth;

public sealed class AuthenticationService(
    DialerDbContext dbContext,
    IDateTimeProvider dateTimeProvider,
    IPasswordHasher passwordHasher,
    IWorkspaceKeyGenerator workspaceKeyGenerator,
    IAccessTokenService accessTokenService,
    ISubscriptionAccessEvaluator subscriptionAccessEvaluator,
    IOptions<SubscriptionOptions> subscriptionOptions) : IAuthenticationService
{
    private readonly int _trialDays = Math.Max(1, subscriptionOptions.Value.TrialDays);

    public async Task<SessionDto> RegisterAdminAsync(AdminSignupRequest request, CancellationToken cancellationToken)
    {
        ValidateAdminSignupRequest(request);

        var email = NormalizeEmail(request.Email);
        if (await dbContext.Users.AnyAsync(x => x.Email == email, cancellationToken))
        {
            throw new InvalidOperationException("An account with this email already exists.");
        }

        var nowUtc = dateTimeProvider.UtcNow;
        var workspaceKey = await GenerateUniqueWorkspaceKeyAsync(request.CompanyName, cancellationToken);

        var tenant = new Tenant
        {
            WorkspaceKey = workspaceKey,
            CompanyName = request.CompanyName.Trim(),
            OwnerName = request.FullName.Trim(),
            OwnerEmail = email,
            OwnerPhoneNumber = request.PhoneNumber.Trim(),
            TimeZoneId = "UTC",
            CreatedAtUtc = nowUtc,
            IsActive = true,
        };

        var adminUser = new ApplicationUser
        {
            Tenant = tenant,
            Username = GenerateAdminUsername(request.Email),
            Email = email,
            FullName = request.FullName.Trim(),
            PhoneNumber = request.PhoneNumber.Trim(),
            PasswordHash = passwordHasher.HashPassword(request.Password),
            Role = UserRole.Admin,
            CanCopyLeadData = true,
            CreatedAtUtc = nowUtc,
            IsEnabled = true,
        };

        var subscription = new TenantSubscription
        {
            Tenant = tenant,
            PlanName = "15-Day Trial",
            Status = SubscriptionStatus.Trial,
            TrialStartedAtUtc = nowUtc,
            TrialEndsAtUtc = nowUtc.AddDays(_trialDays),
            CreatedAtUtc = nowUtc,
            Notes = "Created from admin self-signup.",
        };

        dbContext.Tenants.Add(tenant);
        dbContext.Users.Add(adminUser);
        dbContext.Subscriptions.Add(subscription);

        await dbContext.SaveChangesAsync(cancellationToken);

        return CreateSession(adminUser, tenant, subscription, nowUtc);
    }

    public async Task<SessionDto?> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        ValidateLoginRequest(request);

        var nowUtc = dateTimeProvider.UtcNow;
        var identity = request.UsernameOrEmail.Trim();
        var workspaceKey = NormalizeWorkspaceKey(request.WorkspaceKey);

        ApplicationUser? user;

        if (identity.Contains('@'))
        {
            var email = NormalizeEmail(identity);
            user = await dbContext.Users
                .Include(x => x.Tenant)
                .SingleOrDefaultAsync(
                    x => x.Email == email
                        && (workspaceKey == null || x.Tenant!.WorkspaceKey == workspaceKey),
                    cancellationToken);
        }
        else
        {
            if (string.IsNullOrWhiteSpace(workspaceKey))
            {
                throw new InvalidOperationException("Workspace key is required when signing in with a username.");
            }

            var username = identity.ToLowerInvariant();
            user = await dbContext.Users
                .Include(x => x.Tenant)
                .SingleOrDefaultAsync(
                    x => x.Username == username && x.Tenant!.WorkspaceKey == workspaceKey,
                    cancellationToken);
        }

        if (user is null || !user.IsEnabled || !passwordHasher.VerifyPassword(request.Password, user.PasswordHash))
        {
            return null;
        }

        var tenant = user.Tenant
            ?? await dbContext.Tenants.SingleAsync(x => x.Id == user.TenantId, cancellationToken);

        var subscription = await dbContext.Subscriptions
            .Where(x => x.TenantId == user.TenantId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken)
            ?? CreateExpiredPlaceholderSubscription(user.TenantId, nowUtc);

        user.LastLoginAtUtc = nowUtc;
        user.UpdatedAtUtc = nowUtc;
        await dbContext.SaveChangesAsync(cancellationToken);

        return CreateSession(user, tenant, subscription, nowUtc);
    }

    private SessionDto CreateSession(
        ApplicationUser user,
        Tenant tenant,
        TenantSubscription subscription,
        DateTimeOffset nowUtc)
    {
        var access = subscriptionAccessEvaluator.Evaluate(subscription, nowUtc);
        var accessToken = accessTokenService.CreateAccessToken(new AccessTokenRequest(
            UserId: user.Id,
            TenantId: tenant.Id,
            WorkspaceKey: tenant.WorkspaceKey,
            Email: user.Email,
            FullName: user.FullName,
            Role: user.Role));

        return new SessionDto(
            UserId: user.Id,
            TenantId: tenant.Id,
            FullName: user.FullName,
            CompanyName: tenant.CompanyName,
            WorkspaceKey: tenant.WorkspaceKey,
            AccessToken: accessToken,
            Role: user.Role,
            TimeZoneId: tenant.TimeZoneId,
            SubscriptionStatus: access.EffectiveStatus,
            CanUseDialer: access.CanUseDialer,
            CanViewData: access.CanViewData,
            SubscriptionMessage: access.Reason);
    }

    private async Task<string> GenerateUniqueWorkspaceKeyAsync(string companyName, CancellationToken cancellationToken)
    {
        var baseKey = workspaceKeyGenerator.Generate(companyName);
        var candidate = baseKey;
        var suffix = 2;

        while (await dbContext.Tenants.AnyAsync(x => x.WorkspaceKey == candidate, cancellationToken))
        {
            candidate = $"{baseKey}-{suffix++}";
        }

        return candidate;
    }

    private static string GenerateAdminUsername(string email)
    {
        var localPart = NormalizeEmail(email).Split('@')[0];
        return string.IsNullOrWhiteSpace(localPart) ? "admin" : localPart;
    }

    private static string NormalizeEmail(string email)
    {
        return email.Trim().ToLowerInvariant();
    }

    private static string? NormalizeWorkspaceKey(string? workspaceKey)
    {
        return string.IsNullOrWhiteSpace(workspaceKey)
            ? null
            : workspaceKey.Trim().ToLowerInvariant();
    }

    private static void ValidateAdminSignupRequest(AdminSignupRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.FullName)
            || string.IsNullOrWhiteSpace(request.Email)
            || string.IsNullOrWhiteSpace(request.CompanyName)
            || string.IsNullOrWhiteSpace(request.Password)
            || string.IsNullOrWhiteSpace(request.PhoneNumber))
        {
            throw new InvalidOperationException("Full name, email, company name, password, and phone number are required.");
        }

        if (request.Password.Trim().Length < 8)
        {
            throw new InvalidOperationException("Password must be at least 8 characters long.");
        }
    }

    private static void ValidateLoginRequest(LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.UsernameOrEmail) || string.IsNullOrWhiteSpace(request.Password))
        {
            throw new InvalidOperationException("Username or email and password are required.");
        }
    }

    private static TenantSubscription CreateExpiredPlaceholderSubscription(Guid tenantId, DateTimeOffset nowUtc)
    {
        return new TenantSubscription
        {
            TenantId = tenantId,
            Status = SubscriptionStatus.Expired,
            TrialStartedAtUtc = nowUtc,
            TrialEndsAtUtc = nowUtc,
            PlanName = "Unavailable",
        };
    }
}
