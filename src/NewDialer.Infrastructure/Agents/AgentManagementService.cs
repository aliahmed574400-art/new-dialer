using System.Text;
using Microsoft.EntityFrameworkCore;
using NewDialer.Application.Abstractions;
using NewDialer.Contracts.Agents;
using NewDialer.Domain.Entities;
using NewDialer.Domain.Enums;
using NewDialer.Infrastructure.Persistence;

namespace NewDialer.Infrastructure.Agents;

public sealed class AgentManagementService(
    DialerDbContext dbContext,
    IDateTimeProvider dateTimeProvider,
    IPasswordHasher passwordHasher) : IAgentManagementService
{
    public async Task<IReadOnlyList<AgentAdminDto>> GetAgentsAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        return await BuildAgentSummariesAsync(tenantId, selectedAgentId: null, cancellationToken);
    }

    public async Task<AgentAdminDto> CreateAgentAsync(Guid tenantId, CreateAgentRequest request, CancellationToken cancellationToken)
    {
        ValidateCreateRequest(request);

        var email = NormalizeEmail(request.Email);
        if (await dbContext.Users.AnyAsync(x => x.Email == email, cancellationToken))
        {
            throw new InvalidOperationException("An account with this email already exists.");
        }

        var nowUtc = dateTimeProvider.UtcNow;
        var agent = new ApplicationUser
        {
            TenantId = tenantId,
            FullName = request.FullName.Trim(),
            Email = email,
            Username = await GenerateUniqueUsernameAsync(tenantId, email, request.FullName, existingUserId: null, cancellationToken),
            PasswordHash = passwordHasher.HashPassword(request.Password),
            Role = UserRole.Agent,
            CanCopyLeadData = false,
            IsEnabled = true,
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc,
        };

        dbContext.Users.Add(agent);
        await dbContext.SaveChangesAsync(cancellationToken);

        return await GetAgentOrThrowAsync(tenantId, agent.Id, cancellationToken);
    }

    public async Task<AgentAdminDto> UpdateAgentAsync(Guid tenantId, Guid agentId, UpdateAgentRequest request, CancellationToken cancellationToken)
    {
        ValidateUpdateRequest(request);

        var agent = await dbContext.Users.SingleOrDefaultAsync(
            x => x.Id == agentId
                && x.TenantId == tenantId
                && x.Role == UserRole.Agent
                && x.IsEnabled,
            cancellationToken);

        if (agent is null)
        {
            throw new InvalidOperationException("The selected agent was not found in this workspace.");
        }

        var email = NormalizeEmail(request.Email);
        if (await dbContext.Users.AnyAsync(x => x.Email == email && x.Id != agentId, cancellationToken))
        {
            throw new InvalidOperationException("An account with this email already exists.");
        }

        agent.FullName = request.FullName.Trim();
        agent.Email = email;
        agent.Username = await GenerateUniqueUsernameAsync(tenantId, email, request.FullName, agentId, cancellationToken);
        agent.UpdatedAtUtc = dateTimeProvider.UtcNow;

        if (!string.IsNullOrWhiteSpace(request.Password))
        {
            if (request.Password.Trim().Length < 8)
            {
                throw new InvalidOperationException("Password must be at least 8 characters long.");
            }

            agent.PasswordHash = passwordHasher.HashPassword(request.Password);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return await GetAgentOrThrowAsync(tenantId, agentId, cancellationToken);
    }

    public async Task DeleteAgentAsync(Guid tenantId, Guid agentId, CancellationToken cancellationToken)
    {
        var agent = await dbContext.Users.SingleOrDefaultAsync(
            x => x.Id == agentId
                && x.TenantId == tenantId
                && x.Role == UserRole.Agent
                && x.IsEnabled,
            cancellationToken);

        if (agent is null)
        {
            throw new InvalidOperationException("The selected agent was not found in this workspace.");
        }

        var nowUtc = dateTimeProvider.UtcNow;
        agent.IsEnabled = false;
        agent.UpdatedAtUtc = nowUtc;

        var openSessions = await dbContext.WorkSessions
            .Where(x => x.TenantId == tenantId && x.AgentId == agentId && x.CheckOutAtUtc == null)
            .ToListAsync(cancellationToken);

        foreach (var session in openSessions)
        {
            session.CheckOutAtUtc = nowUtc;
            session.UpdatedAtUtc = nowUtc;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<AgentAdminDto> GetAgentOrThrowAsync(Guid tenantId, Guid agentId, CancellationToken cancellationToken)
    {
        var agent = (await BuildAgentSummariesAsync(tenantId, agentId, cancellationToken)).SingleOrDefault();
        return agent ?? throw new InvalidOperationException("The selected agent was not found in this workspace.");
    }

    private async Task<IReadOnlyList<AgentAdminDto>> BuildAgentSummariesAsync(Guid tenantId, Guid? selectedAgentId, CancellationToken cancellationToken)
    {
        var tenant = await dbContext.Tenants
            .AsNoTracking()
            .SingleAsync(x => x.Id == tenantId, cancellationToken);

        var (dayStartUtc, dayEndUtc) = ResolveLocalDayBounds(tenant.TimeZoneId);

        var query = dbContext.Users
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.Role == UserRole.Agent && x.IsEnabled);

        if (selectedAgentId.HasValue)
        {
            query = query.Where(x => x.Id == selectedAgentId.Value);
        }

        var agents = await query
            .OrderBy(x => x.FullName)
            .Select(x => new AgentRecord(
                x.Id,
                x.FullName,
                x.Email,
                x.Username,
                x.IsEnabled))
            .ToListAsync(cancellationToken);

        if (agents.Count == 0)
        {
            return [];
        }

        var agentIds = agents.Select(x => x.Id).ToArray();

        var sessions = await dbContext.WorkSessions
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId
                && agentIds.Contains(x.AgentId)
                && x.CheckInAtUtc >= dayStartUtc
                && x.CheckInAtUtc < dayEndUtc)
            .ToListAsync(cancellationToken);

        var scheduledCounts = await dbContext.ScheduledCalls
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId
                && agentIds.Contains(x.AgentId)
                && x.Status == ScheduleStatus.Pending)
            .GroupBy(x => x.AgentId)
            .Select(x => new { AgentId = x.Key, Count = x.Count() })
            .ToDictionaryAsync(x => x.AgentId, x => x.Count, cancellationToken);

        return agents
            .Select(agent =>
            {
                var agentSessions = sessions.Where(x => x.AgentId == agent.Id).ToList();
                scheduledCounts.TryGetValue(agent.Id, out var scheduledCalls);

                return new AgentAdminDto(
                    agent.Id,
                    agent.FullName,
                    agent.Email,
                    agent.Username,
                    agent.IsEnabled,
                    agentSessions.Sum(x => x.TotalCalls),
                    scheduledCalls,
                    agentSessions.Sum(x => x.TotalTalkSeconds),
                    agentSessions.Count == 0 ? null : agentSessions.Min(x => x.CheckInAtUtc),
                    agentSessions.Where(x => x.CheckOutAtUtc.HasValue).Select(x => x.CheckOutAtUtc).OrderByDescending(x => x).FirstOrDefault());
            })
            .ToList();
    }

    private async Task<string> GenerateUniqueUsernameAsync(
        Guid tenantId,
        string email,
        string fullName,
        Guid? existingUserId,
        CancellationToken cancellationToken)
    {
        var baseUsername = BuildUsernameBase(email, fullName);
        var candidate = baseUsername;
        var suffix = 2;

        while (await dbContext.Users.AnyAsync(
                   x => x.TenantId == tenantId
                       && x.Username == candidate
                       && (!existingUserId.HasValue || x.Id != existingUserId.Value),
                   cancellationToken))
        {
            candidate = $"{baseUsername}{suffix++}";
        }

        return candidate;
    }

    private (DateTimeOffset DayStartUtc, DateTimeOffset DayEndUtc) ResolveLocalDayBounds(string? timeZoneId)
    {
        var timeZone = ResolveTimeZone(timeZoneId);
        var nowLocal = TimeZoneInfo.ConvertTime(dateTimeProvider.UtcNow, timeZone);
        var dayStartLocal = new DateTime(nowLocal.Year, nowLocal.Month, nowLocal.Day, 0, 0, 0, DateTimeKind.Unspecified);
        var dayEndLocal = dayStartLocal.AddDays(1);

        return (
            new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(dayStartLocal, timeZone), TimeSpan.Zero),
            new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(dayEndLocal, timeZone), TimeSpan.Zero));
    }

    private static TimeZoneInfo ResolveTimeZone(string? timeZoneId)
    {
        if (!string.IsNullOrWhiteSpace(timeZoneId))
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            }
            catch (TimeZoneNotFoundException)
            {
            }
            catch (InvalidTimeZoneException)
            {
            }
        }

        return TimeZoneInfo.Utc;
    }

    private static void ValidateCreateRequest(CreateAgentRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.FullName)
            || string.IsNullOrWhiteSpace(request.Email)
            || string.IsNullOrWhiteSpace(request.Password))
        {
            throw new InvalidOperationException("Agent name, email, and password are required.");
        }

        if (request.Password.Trim().Length < 8)
        {
            throw new InvalidOperationException("Password must be at least 8 characters long.");
        }
    }

    private static void ValidateUpdateRequest(UpdateAgentRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.FullName) || string.IsNullOrWhiteSpace(request.Email))
        {
            throw new InvalidOperationException("Agent name and email are required.");
        }
    }

    private static string NormalizeEmail(string email)
    {
        return email.Trim().ToLowerInvariant();
    }

    private static string BuildUsernameBase(string email, string fullName)
    {
        var localPart = NormalizeEmail(email).Split('@', 2)[0];
        var source = string.IsNullOrWhiteSpace(localPart) ? fullName : localPart;
        var builder = new StringBuilder();
        var previousWasSeparator = false;

        foreach (var character in source.Trim())
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(char.ToLowerInvariant(character));
                previousWasSeparator = false;
                continue;
            }

            if (!previousWasSeparator && builder.Length > 0)
            {
                builder.Append('.');
                previousWasSeparator = true;
            }
        }

        var result = builder.ToString().Trim('.');
        return string.IsNullOrWhiteSpace(result) ? "agent" : result;
    }

    private sealed record AgentRecord(
        Guid Id,
        string FullName,
        string Email,
        string Username,
        bool IsEnabled);
}
