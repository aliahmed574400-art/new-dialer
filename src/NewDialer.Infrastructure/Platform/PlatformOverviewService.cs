using Microsoft.EntityFrameworkCore;
using NewDialer.Application.Abstractions;
using NewDialer.Contracts.Platform;
using NewDialer.Infrastructure.Persistence;

namespace NewDialer.Infrastructure.Platform;

public sealed class PlatformOverviewService(DialerDbContext dbContext) : IPlatformOverviewService
{
    public async Task<IReadOnlyList<TenantOverviewDto>> GetTenantOverviewAsync(CancellationToken cancellationToken)
    {
        var tenants = await dbContext.Tenants
            .AsNoTracking()
            .OrderBy(x => x.CompanyName)
            .Select(x => new
            {
                x.Id,
                x.CompanyName,
                x.OwnerEmail,
            })
            .ToListAsync(cancellationToken);

        var latestSubscriptions = await dbContext.Subscriptions
            .AsNoTracking()
            .GroupBy(x => x.TenantId)
            .Select(group => group
                .OrderByDescending(x => x.CreatedAtUtc)
                .Select(x => new
                {
                    x.TenantId,
                    x.PlanName,
                    x.Status,
                    x.TrialEndsAtUtc,
                    x.CurrentPeriodEndsAtUtc,
                })
                .First())
            .ToDictionaryAsync(x => x.TenantId, cancellationToken);

        return tenants
            .Select(tenant =>
            {
                if (latestSubscriptions.TryGetValue(tenant.Id, out var subscription))
                {
                    return new TenantOverviewDto(
                        tenant.Id,
                        tenant.CompanyName,
                        tenant.OwnerEmail,
                        subscription.PlanName,
                        subscription.Status,
                        subscription.TrialEndsAtUtc,
                        subscription.CurrentPeriodEndsAtUtc);
                }

                return new TenantOverviewDto(
                    tenant.Id,
                    tenant.CompanyName,
                    tenant.OwnerEmail,
                    "Unavailable",
                    Domain.Enums.SubscriptionStatus.Expired,
                    null,
                    null);
            })
            .ToList();
    }
}
