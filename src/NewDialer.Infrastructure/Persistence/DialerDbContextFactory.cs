using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace NewDialer.Infrastructure.Persistence;

public sealed class DialerDbContextFactory : IDesignTimeDbContextFactory<DialerDbContext>
{
    public DialerDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<DialerDbContext>();
        var connectionString =
            Environment.GetEnvironmentVariable("NEWDIALER_CONNECTION_STRING")
            ?? "Host=localhost;Port=5432;Database=newdialer;Username=postgres;Password=postgres";

        optionsBuilder.UseNpgsql(connectionString);
        return new DialerDbContext(optionsBuilder.Options);
    }
}
