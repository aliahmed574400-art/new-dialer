using Microsoft.EntityFrameworkCore;
using NewDialer.Application.Abstractions;
using NewDialer.Application.Models;
using NewDialer.Contracts.Leads;
using NewDialer.Domain.Entities;
using NewDialer.Domain.Enums;
using NewDialer.Infrastructure.Persistence;

namespace NewDialer.Infrastructure.Leads;

public sealed class LeadManagementService(
    DialerDbContext dbContext,
    ILeadSpreadsheetService leadSpreadsheetService,
    IDateTimeProvider dateTimeProvider) : ILeadManagementService
{
    private const string ExcelContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    public async Task<LeadImportResultDto> ImportLeadsAsync(LeadImportCommand command, Stream fileStream, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.FileName))
        {
            throw new InvalidOperationException("A file name is required.");
        }

        ApplicationUser? defaultAgent = null;
        if (command.DefaultAgentId.HasValue)
        {
            defaultAgent = await dbContext.Users.SingleOrDefaultAsync(
                x => x.Id == command.DefaultAgentId.Value
                    && x.TenantId == command.TenantId
                    && x.Role == UserRole.Agent
                    && x.IsEnabled,
                cancellationToken);

            if (defaultAgent is null)
            {
                throw new InvalidOperationException("The selected default agent was not found in this workspace.");
            }
        }

        var spreadsheetResult = await leadSpreadsheetService.ReadLeadsAsync(fileStream, cancellationToken);
        var nowUtc = dateTimeProvider.UtcNow;
        var existingPhones = await dbContext.Leads
            .Where(x => x.TenantId == command.TenantId)
            .Select(x => x.PhoneNumber)
            .ToListAsync(cancellationToken);

        var knownPhones = new HashSet<string>(existingPhones.Select(NormalizePhone), StringComparer.OrdinalIgnoreCase);
        var batch = new LeadImportBatch
        {
            TenantId = command.TenantId,
            UploadedByUserId = command.UploadedByUserId,
            FileName = command.FileName.Trim(),
            TotalRows = spreadsheetResult.TotalRows,
            Notes = command.Notes.Trim(),
            ImportedAtUtc = nowUtc,
            CreatedAtUtc = nowUtc,
        };

        var importedCount = 0;
        var skippedRows = spreadsheetResult.SkippedRows;

        foreach (var draft in spreadsheetResult.Leads)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var normalizedPhone = NormalizePhone(draft.PhoneNumber);
            if (!knownPhones.Add(normalizedPhone))
            {
                skippedRows++;
                continue;
            }

            dbContext.Leads.Add(new Lead
            {
                TenantId = command.TenantId,
                ImportBatch = batch,
                AssignedAgentId = defaultAgent?.Id,
                Name = draft.Name.Trim(),
                Email = draft.Email.Trim(),
                PhoneNumber = draft.PhoneNumber.Trim(),
                Website = draft.Website.Trim(),
                Service = draft.Service.Trim(),
                Budget = draft.Budget.Trim(),
                Status = defaultAgent is null ? LeadStatus.New : LeadStatus.Queued,
                CreatedAtUtc = nowUtc,
            });

            importedCount++;
        }

        batch.ImportedRows = importedCount;
        dbContext.LeadImportBatches.Add(batch);
        await dbContext.SaveChangesAsync(cancellationToken);

        var message = importedCount == 0
            ? "No new leads were imported. Check for duplicate phone numbers or empty rows."
            : $"Imported {importedCount} lead(s) from {command.FileName}.";

        return new LeadImportResultDto(
            BatchId: batch.Id,
            FileName: batch.FileName,
            TotalRows: batch.TotalRows,
            ImportedRows: importedCount,
            SkippedRows: skippedRows,
            DefaultAgentId: defaultAgent?.Id,
            Message: message);
    }

    public async Task AssignLeadsAsync(AssignLeadsCommand command, CancellationToken cancellationToken)
    {
        if (command.LeadIds.Count == 0)
        {
            throw new InvalidOperationException("Select at least one lead to assign.");
        }

        var agent = await dbContext.Users.SingleOrDefaultAsync(
            x => x.Id == command.AgentId
                && x.TenantId == command.TenantId
                && x.Role == UserRole.Agent
                && x.IsEnabled,
            cancellationToken);

        if (agent is null)
        {
            throw new InvalidOperationException("The selected agent was not found in this workspace.");
        }

        var leads = await dbContext.Leads
            .Where(x => x.TenantId == command.TenantId && command.LeadIds.Contains(x.Id))
            .ToListAsync(cancellationToken);

        if (leads.Count != command.LeadIds.Count)
        {
            throw new InvalidOperationException("One or more selected leads were not found in this workspace.");
        }

        var nowUtc = dateTimeProvider.UtcNow;
        foreach (var lead in leads)
        {
            lead.AssignedAgentId = command.AgentId;
            lead.UpdatedAtUtc = nowUtc;

            if (lead.Status == LeadStatus.New)
            {
                lead.Status = LeadStatus.Queued;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<IReadOnlyList<LeadDto>> GetTenantLeadsAsync(Guid tenantId, Guid? assignedAgentId, CancellationToken cancellationToken)
    {
        return QueryLeadDtos(tenantId, assignedAgentId, cancellationToken);
    }

    public Task<IReadOnlyList<LeadDto>> GetAssignedLeadsAsync(Guid tenantId, Guid agentId, CancellationToken cancellationToken)
    {
        return QueryLeadDtos(tenantId, agentId, cancellationToken);
    }

    public async Task<IReadOnlyList<LeadImportBatchDto>> GetImportBatchesAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        return await dbContext.LeadImportBatches
            .Where(x => x.TenantId == tenantId)
            .OrderByDescending(x => x.ImportedAtUtc)
            .Select(x => new LeadImportBatchDto(
                x.Id,
                x.FileName,
                x.TotalRows,
                x.ImportedRows,
                x.Notes,
                x.ImportedAtUtc,
                x.UploadedByUserId))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AgentAssignmentOptionDto>> GetAgentOptionsAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        return await dbContext.Users
            .Where(x => x.TenantId == tenantId && x.Role == UserRole.Agent)
            .OrderBy(x => x.FullName)
            .Select(x => new AgentAssignmentOptionDto(
                x.Id,
                x.FullName,
                x.Username,
                x.IsEnabled))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ScheduledCallListItemDto>> GetScheduledCallsAsync(Guid tenantId, Guid? agentId, CancellationToken cancellationToken)
    {
        var query = dbContext.ScheduledCalls
            .AsNoTracking()
            .Include(x => x.Lead)
            .Include(x => x.Agent)
            .Where(x => x.TenantId == tenantId);

        if (agentId.HasValue)
        {
            query = query.Where(x => x.AgentId == agentId.Value);
        }

        return await query
            .OrderBy(x => x.ScheduledForUtc)
            .Select(x => new ScheduledCallListItemDto(
                x.Id,
                x.LeadId,
                x.Lead != null ? x.Lead.Name : string.Empty,
                x.Lead != null ? x.Lead.PhoneNumber : string.Empty,
                x.AgentId,
                x.Agent != null ? x.Agent.FullName : string.Empty,
                x.ScheduledForUtc,
                x.TimeZoneId,
                x.Notes,
                x.Status))
            .ToListAsync(cancellationToken);
    }

    public async Task<ScheduledCallListItemDto> CreateScheduledCallAsync(CreateScheduledCallCommand command, CancellationToken cancellationToken)
    {
        if (command.ScheduledForUtc <= dateTimeProvider.UtcNow.AddMinutes(-1))
        {
            throw new InvalidOperationException("Schedule date and time must be in the future.");
        }

        var agent = await dbContext.Users.SingleOrDefaultAsync(
            x => x.Id == command.AgentId
                && x.TenantId == command.TenantId
                && x.IsEnabled,
            cancellationToken);

        if (agent is null)
        {
            throw new InvalidOperationException("The selected agent is not available in this workspace.");
        }

        var lead = await dbContext.Leads
            .Include(x => x.AssignedAgent)
            .SingleOrDefaultAsync(
                x => x.Id == command.LeadId
                    && x.TenantId == command.TenantId,
                cancellationToken);

        if (lead is null)
        {
            throw new InvalidOperationException("The selected lead was not found in this workspace.");
        }

        if (command.RequireAssignedLeadOwnership && lead.AssignedAgentId != command.AgentId)
        {
            throw new InvalidOperationException("Agents can only schedule callbacks for leads assigned to them.");
        }

        var nowUtc = dateTimeProvider.UtcNow;
        var schedule = new ScheduledCall
        {
            TenantId = command.TenantId,
            LeadId = lead.Id,
            AgentId = agent.Id,
            ScheduledForUtc = command.ScheduledForUtc,
            TimeZoneId = string.IsNullOrWhiteSpace(command.TimeZoneId) ? "UTC" : command.TimeZoneId.Trim(),
            Notes = command.Notes.Trim(),
            Status = ScheduleStatus.Pending,
            CreatedAtUtc = nowUtc,
        };

        lead.Status = LeadStatus.FollowUpScheduled;
        lead.UpdatedAtUtc = nowUtc;

        dbContext.ScheduledCalls.Add(schedule);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new ScheduledCallListItemDto(
            schedule.Id,
            lead.Id,
            lead.Name,
            lead.PhoneNumber,
            agent.Id,
            agent.FullName,
            schedule.ScheduledForUtc,
            schedule.TimeZoneId,
            schedule.Notes,
            schedule.Status);
    }

    public async Task<ScheduleExportDocument> ExportSchedulesAsync(ScheduleExportRequest request, CancellationToken cancellationToken)
    {
        var query = dbContext.ScheduledCalls
            .AsNoTracking()
            .Include(x => x.Lead)
            .Include(x => x.Agent)
            .Where(x => x.TenantId == request.TenantId);

        if (request.AgentId.HasValue)
        {
            query = query.Where(x => x.AgentId == request.AgentId.Value);
        }

        if (request.FromUtc.HasValue)
        {
            query = query.Where(x => x.ScheduledForUtc >= request.FromUtc.Value);
        }

        if (request.ToUtc.HasValue)
        {
            query = query.Where(x => x.ScheduledForUtc <= request.ToUtc.Value);
        }

        var rows = await query
            .OrderBy(x => x.ScheduledForUtc)
            .Select(x => new ScheduleExportRow(
                x.Lead != null ? x.Lead.Name : string.Empty,
                x.Lead != null ? x.Lead.PhoneNumber : string.Empty,
                x.Agent != null ? x.Agent.FullName : string.Empty,
                x.TimeZoneId,
                x.ScheduledForUtc,
                x.Notes))
            .ToListAsync(cancellationToken);

        var content = await leadSpreadsheetService.ExportSchedulesAsync(rows, cancellationToken);
        return new ScheduleExportDocument(
            FileName: $"scheduled-calls-{dateTimeProvider.UtcNow:yyyyMMddHHmmss}.xlsx",
            ContentType: ExcelContentType,
            Content: content);
    }

    private async Task<IReadOnlyList<LeadDto>> QueryLeadDtos(Guid tenantId, Guid? assignedAgentId, CancellationToken cancellationToken)
    {
        var query = dbContext.Leads
            .AsNoTracking()
            .Include(x => x.AssignedAgent)
            .Where(x => x.TenantId == tenantId);

        if (assignedAgentId.HasValue)
        {
            query = query.Where(x => x.AssignedAgentId == assignedAgentId.Value);
        }

        return await query
            .OrderByDescending(x => x.CreatedAtUtc)
            .Select(x => new LeadDto(
                x.Id,
                x.Name,
                x.Email,
                x.PhoneNumber,
                x.Website,
                x.Service,
                x.Budget,
                x.Status,
                x.AssignedAgentId,
                x.AssignedAgent != null ? x.AssignedAgent.FullName : null,
                x.ImportBatchId))
            .ToListAsync(cancellationToken);
    }

    private static string NormalizePhone(string phoneNumber)
    {
        return new string(phoneNumber.Where(char.IsLetterOrDigit).ToArray());
    }
}
