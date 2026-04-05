using Microsoft.AspNetCore.Http;

namespace NewDialer.Api.Contracts;

public sealed class LeadImportForm
{
    public IFormFile? File { get; set; }

    public string Notes { get; set; } = string.Empty;

    public Guid? DefaultAgentId { get; set; }
}
