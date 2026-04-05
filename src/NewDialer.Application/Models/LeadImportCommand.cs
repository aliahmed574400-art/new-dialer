namespace NewDialer.Application.Models;

public sealed record LeadImportCommand(
    Guid TenantId,
    Guid UploadedByUserId,
    string FileName,
    string Notes,
    Guid? DefaultAgentId);
