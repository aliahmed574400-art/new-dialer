using Microsoft.EntityFrameworkCore;
using NewDialer.Domain.Entities;

namespace NewDialer.Infrastructure.Persistence;

public sealed class DialerDbContext(DbContextOptions<DialerDbContext> options) : DbContext(options)
{
    public DbSet<Tenant> Tenants => Set<Tenant>();

    public DbSet<ApplicationUser> Users => Set<ApplicationUser>();

    public DbSet<LeadImportBatch> LeadImportBatches => Set<LeadImportBatch>();

    public DbSet<Lead> Leads => Set<Lead>();

    public DbSet<ScheduledCall> ScheduledCalls => Set<ScheduledCall>();

    public DbSet<CallAttempt> CallAttempts => Set<CallAttempt>();

    public DbSet<WorkSession> WorkSessions => Set<WorkSession>();

    public DbSet<DialerRun> DialerRuns => Set<DialerRun>();

    public DbSet<TenantSubscription> Subscriptions => Set<TenantSubscription>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Tenant>(entity =>
        {
            entity.ToTable("tenants");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.WorkspaceKey).IsUnique();
            entity.Property(x => x.WorkspaceKey).HasMaxLength(80);
            entity.Property(x => x.CompanyName).HasMaxLength(200);
            entity.Property(x => x.OwnerName).HasMaxLength(150);
            entity.Property(x => x.OwnerEmail).HasMaxLength(200);
            entity.Property(x => x.OwnerPhoneNumber).HasMaxLength(50);
            entity.Property(x => x.TimeZoneId).HasMaxLength(100);
        });

        modelBuilder.Entity<ApplicationUser>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.TenantId, x.Username }).IsUnique();
            entity.HasIndex(x => x.Email).IsUnique();
            entity.Property(x => x.FullName).HasMaxLength(150);
            entity.Property(x => x.Username).HasMaxLength(80);
            entity.Property(x => x.Email).HasMaxLength(200);
            entity.Property(x => x.PhoneNumber).HasMaxLength(50);
            entity.Property(x => x.PasswordHash).HasMaxLength(500);
            entity.Property(x => x.Role).HasConversion<string>().HasMaxLength(24);
            entity.HasOne(x => x.Tenant)
                .WithMany(x => x.Users)
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<LeadImportBatch>(entity =>
        {
            entity.ToTable("lead_import_batches");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.FileName).HasMaxLength(260);
            entity.Property(x => x.Notes).HasMaxLength(2000);
            entity.HasOne(x => x.Tenant)
                .WithMany(x => x.LeadImportBatches)
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.UploadedByUser)
                .WithMany()
                .HasForeignKey(x => x.UploadedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Lead>(entity =>
        {
            entity.ToTable("leads");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.TenantId, x.PhoneNumber });
            entity.Property(x => x.Name).HasMaxLength(200);
            entity.Property(x => x.Email).HasMaxLength(200);
            entity.Property(x => x.PhoneNumber).HasMaxLength(50);
            entity.Property(x => x.Website).HasMaxLength(300);
            entity.Property(x => x.Service).HasMaxLength(120);
            entity.Property(x => x.Budget).HasMaxLength(80);
            entity.Property(x => x.LastOutcome).HasMaxLength(250);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
            entity.HasOne(x => x.Tenant)
                .WithMany(x => x.Leads)
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.ImportBatch)
                .WithMany(x => x.Leads)
                .HasForeignKey(x => x.ImportBatchId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.AssignedAgent)
                .WithMany(x => x.AssignedLeads)
                .HasForeignKey(x => x.AssignedAgentId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<ScheduledCall>(entity =>
        {
            entity.ToTable("scheduled_calls");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.TenantId, x.ScheduledForUtc });
            entity.Property(x => x.TimeZoneId).HasMaxLength(100);
            entity.Property(x => x.Notes).HasMaxLength(2000);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
            entity.HasOne(x => x.Tenant)
                .WithMany(x => x.ScheduledCalls)
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Lead)
                .WithMany(x => x.ScheduledCalls)
                .HasForeignKey(x => x.LeadId);
            entity.HasOne(x => x.Agent)
                .WithMany(x => x.ScheduledCalls)
                .HasForeignKey(x => x.AgentId);
        });

        modelBuilder.Entity<CallAttempt>(entity =>
        {
            entity.ToTable("call_attempts");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.TenantId, x.StartedAtUtc });
            entity.Property(x => x.ExternalCallId).HasMaxLength(150);
            entity.Property(x => x.Notes).HasMaxLength(2000);
            entity.Property(x => x.Disposition).HasConversion<string>().HasMaxLength(32);
            entity.HasOne(x => x.Tenant)
                .WithMany()
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Lead)
                .WithMany(x => x.CallAttempts)
                .HasForeignKey(x => x.LeadId);
            entity.HasOne(x => x.Agent)
                .WithMany(x => x.CallAttempts)
                .HasForeignKey(x => x.AgentId);
        });

        modelBuilder.Entity<WorkSession>(entity =>
        {
            entity.ToTable("work_sessions");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.TenantId, x.AgentId, x.CheckInAtUtc });
            entity.HasOne(x => x.Tenant)
                .WithMany()
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Agent)
                .WithMany(x => x.WorkSessions)
                .HasForeignKey(x => x.AgentId);
        });

        modelBuilder.Entity<DialerRun>(entity =>
        {
            entity.ToTable("dialer_runs");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.TenantId, x.AgentId, x.StartedAtUtc });
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
            entity.HasOne(x => x.Tenant)
                .WithMany()
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Agent)
                .WithMany(x => x.DialerRuns)
                .HasForeignKey(x => x.AgentId);
        });

        modelBuilder.Entity<TenantSubscription>(entity =>
        {
            entity.ToTable("tenant_subscriptions");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.TenantId, x.Status });
            entity.Property(x => x.PlanName).HasMaxLength(80);
            entity.Property(x => x.PaymentReference).HasMaxLength(120);
            entity.Property(x => x.Notes).HasMaxLength(2000);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
            entity.HasOne(x => x.Tenant)
                .WithMany(x => x.Subscriptions)
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        base.OnModelCreating(modelBuilder);
    }
}
