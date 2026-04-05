using NewDialer.Contracts.Platform;

namespace NewDialer.Application.Abstractions;

public interface IPlatformOverviewService
{
    Task<IReadOnlyList<TenantOverviewDto>> GetTenantOverviewAsync(CancellationToken cancellationToken);
}
