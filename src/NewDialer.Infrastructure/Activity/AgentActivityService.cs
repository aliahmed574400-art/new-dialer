using Microsoft.EntityFrameworkCore;
using NewDialer.Application.Abstractions;
using NewDialer.Contracts.Analytics;
using NewDialer.Domain.Entities;
using NewDialer.Domain.Enums;
using NewDialer.Infrastructure.Persistence;

namespace NewDialer.Infrastructure.Activity;

public sealed class AgentActivityService(
    DialerDbContext dbContext,
    IDateTimeProvider dateTimeProvider) : IAgentActivityService
{
    public async Task RecordSignInAsync(Guid tenantId, Guid userId, UserRole role, CancellationToken cancellationToken)
    {
        if (role != UserRole.Agent)
        {
            return;
        }

        var existingOpenSession = await dbContext.WorkSessions
            .SingleOrDefaultAsync(
                x => x.TenantId == tenantId
                    && x.AgentId == userId
                    && x.CheckOutAtUtc == null,
                cancellationToken);

        if (existingOpenSession is not null)
        {
            return;
        }

        var nowUtc = dateTimeProvider.UtcNow;
        dbContext.WorkSessions.Add(new WorkSession
        {
            TenantId = tenantId,
            AgentId = userId,
            CheckInAtUtc = nowUtc,
            CreatedAtUtc = nowUtc,
        });

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task RecordSignOutAsync(Guid tenantId, Guid userId, UserRole role, CancellationToken cancellationToken)
    {
        if (role != UserRole.Agent)
        {
            return;
        }

        var session = await dbContext.WorkSessions
            .Where(x => x.TenantId == tenantId && x.AgentId == userId && x.CheckOutAtUtc == null)
            .OrderByDescending(x => x.CheckInAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (session is null)
        {
            return;
        }

        session.CheckOutAtUtc = dateTimeProvider.UtcNow;
        session.UpdatedAtUtc = dateTimeProvider.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task RecordCallStartedAsync(Guid tenantId, Guid agentId, Guid leadId, string externalCallId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(externalCallId))
        {
            return;
        }

        await EnsureOpenSessionAsync(tenantId, agentId, cancellationToken);

        var nowUtc = dateTimeProvider.UtcNow;
        var existingAttempt = await dbContext.CallAttempts
            .SingleOrDefaultAsync(
                x => x.TenantId == tenantId && x.ExternalCallId == externalCallId,
                cancellationToken);

        if (existingAttempt is null)
        {
            dbContext.CallAttempts.Add(new CallAttempt
            {
                TenantId = tenantId,
                AgentId = agentId,
                LeadId = leadId,
                ExternalCallId = externalCallId.Trim(),
                StartedAtUtc = nowUtc,
                CreatedAtUtc = nowUtc,
            });
        }

        var lead = await dbContext.Leads.SingleOrDefaultAsync(x => x.Id == leadId && x.TenantId == tenantId, cancellationToken);
        if (lead is not null)
        {
            lead.Status = LeadStatus.Dialing;
            lead.UpdatedAtUtc = nowUtc;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task RecordCallEndedAsync(
        Guid tenantId,
        Guid agentId,
        string externalCallId,
        bool wasAnswered,
        bool requeueLead,
        string? outcomeLabel,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(externalCallId))
        {
            return;
        }

        var attempt = await dbContext.CallAttempts
            .Include(x => x.Lead)
            .Where(x => x.TenantId == tenantId && x.AgentId == agentId && x.ExternalCallId == externalCallId)
            .OrderByDescending(x => x.StartedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (attempt is null || attempt.EndedAtUtc.HasValue)
        {
            return;
        }

        var nowUtc = dateTimeProvider.UtcNow;
        var elapsedSeconds = Math.Max(0, (int)(nowUtc - attempt.StartedAtUtc).TotalSeconds);
        attempt.EndedAtUtc = nowUtc;
        attempt.DurationSeconds = wasAnswered ? elapsedSeconds : 0;
        attempt.WasAnswered = wasAnswered;
        attempt.UpdatedAtUtc = nowUtc;

        if (attempt.Lead is not null)
        {
            attempt.Lead.Status = ResolveLeadStatus(wasAnswered, requeueLead);
            attempt.Lead.LastOutcome = ResolveOutcomeLabel(wasAnswered, requeueLead, outcomeLabel);
            attempt.Lead.UpdatedAtUtc = nowUtc;
        }

        var session = await dbContext.WorkSessions
            .Where(x => x.TenantId == tenantId && x.AgentId == agentId && x.CheckOutAtUtc == null)
            .OrderByDescending(x => x.CheckInAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (session is not null)
        {
            session.TotalCalls += 1;
            session.TotalTalkSeconds += attempt.DurationSeconds;
            session.UpdatedAtUtc = nowUtc;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AgentPerformanceDto>> GetDailyPerformanceAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var tenant = await dbContext.Tenants
            .AsNoTracking()
            .SingleAsync(x => x.Id == tenantId, cancellationToken);

        var timeZone = ResolveTimeZone(tenant.TimeZoneId);
        var nowLocal = TimeZoneInfo.ConvertTime(dateTimeProvider.UtcNow, timeZone);
        var dayStartLocal = new DateTime(nowLocal.Year, nowLocal.Month, nowLocal.Day, 0, 0, 0, DateTimeKind.Unspecified);
        var dayEndLocal = dayStartLocal.AddDays(1);
        var dayStartUtc = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(dayStartLocal, timeZone), TimeSpan.Zero);
        var dayEndUtc = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(dayEndLocal, timeZone), TimeSpan.Zero);

        var agents = await dbContext.Users
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.Role == UserRole.Agent && x.IsEnabled)
            .OrderBy(x => x.FullName)
            .Select(x => new { x.Id, x.FullName })
            .ToListAsync(cancellationToken);

        var sessions = await dbContext.WorkSessions
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.CheckInAtUtc >= dayStartUtc && x.CheckInAtUtc < dayEndUtc)
            .ToListAsync(cancellationToken);

        return agents
            .Select(agent =>
            {
                var agentSessions = sessions.Where(x => x.AgentId == agent.Id).ToList();
                return new AgentPerformanceDto(
                    agent.Id,
                    agent.FullName,
                    agentSessions.Sum(x => x.TotalCalls),
                    agentSessions.Sum(x => x.TotalTalkSeconds),
                    agentSessions.Count == 0 ? null : agentSessions.Min(x => x.CheckInAtUtc),
                    agentSessions.Where(x => x.CheckOutAtUtc.HasValue).Select(x => x.CheckOutAtUtc).OrderByDescending(x => x).FirstOrDefault());
            })
            .ToList();
    }

    private async Task EnsureOpenSessionAsync(Guid tenantId, Guid agentId, CancellationToken cancellationToken)
    {
        var openSession = await dbContext.WorkSessions
            .SingleOrDefaultAsync(
                x => x.TenantId == tenantId
                    && x.AgentId == agentId
                    && x.CheckOutAtUtc == null,
                cancellationToken);

        if (openSession is not null)
        {
            return;
        }

        var nowUtc = dateTimeProvider.UtcNow;
        dbContext.WorkSessions.Add(new WorkSession
        {
            TenantId = tenantId,
            AgentId = agentId,
            CheckInAtUtc = nowUtc,
            CreatedAtUtc = nowUtc,
        });
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

    private static LeadStatus ResolveLeadStatus(bool wasAnswered, bool requeueLead)
    {
        if (requeueLead)
        {
            return LeadStatus.Queued;
        }

        return wasAnswered ? LeadStatus.Completed : LeadStatus.Failed;
    }

    private static string ResolveOutcomeLabel(bool wasAnswered, bool requeueLead, string? outcomeLabel)
    {
        if (!string.IsNullOrWhiteSpace(outcomeLabel))
        {
            return outcomeLabel.Trim();
        }

        if (requeueLead)
        {
            return "Skipped";
        }

        return wasAnswered ? "Answered" : "No answer";
    }
}
