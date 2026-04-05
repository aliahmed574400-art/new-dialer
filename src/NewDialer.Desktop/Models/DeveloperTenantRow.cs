namespace NewDialer.Desktop.Models;

public sealed class DeveloperTenantRow
{
    public required string CompanyName { get; init; }

    public required string AdminEmail { get; init; }

    public required string Plan { get; init; }

    public required string Status { get; init; }

    public required string TrialEnds { get; init; }

    public required string CurrentPeriodEnds { get; init; }
}
