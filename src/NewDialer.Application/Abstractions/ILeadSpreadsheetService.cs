using NewDialer.Application.Models;

namespace NewDialer.Application.Abstractions;

public interface ILeadSpreadsheetService
{
    Task<LeadSpreadsheetReadResult> ReadLeadsAsync(Stream stream, CancellationToken cancellationToken);

    Task<Stream> ExportSchedulesAsync(IEnumerable<ScheduleExportRow> rows, CancellationToken cancellationToken);
}
