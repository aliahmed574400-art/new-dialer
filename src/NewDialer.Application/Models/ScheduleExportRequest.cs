namespace NewDialer.Application.Models;

public sealed record ScheduleExportRequest(
    Guid TenantId,
    Guid? AgentId,
    DateTimeOffset? FromUtc,
    DateTimeOffset? ToUtc);
