using ClosedXML.Excel;
using NewDialer.Application.Abstractions;
using NewDialer.Application.Models;

namespace NewDialer.Infrastructure.Spreadsheets;

public sealed class ClosedXmlLeadSpreadsheetService : ILeadSpreadsheetService
{
    private static readonly string[] RequiredColumns =
    [
        "name",
        "email",
        "phone",
        "website",
        "service",
        "budget",
    ];

    public Task<LeadSpreadsheetReadResult> ReadLeadsAsync(Stream stream, CancellationToken cancellationToken)
    {
        using var workbook = new XLWorkbook(stream);
        var worksheet = workbook.Worksheets.First();
        var headerRow = worksheet.FirstRowUsed();
        var headerMap = BuildHeaderMap(headerRow);
        ValidateRequiredColumns(headerMap);
        var leads = new List<ImportedLeadDraft>();
        var totalRows = 0;
        var skippedRows = 0;

        foreach (var row in worksheet.RowsUsed().Skip(1))
        {
            cancellationToken.ThrowIfCancellationRequested();
            totalRows++;

            var phone = GetValue(row, headerMap, "phone");
            if (string.IsNullOrWhiteSpace(phone))
            {
                skippedRows++;
                continue;
            }

            leads.Add(new ImportedLeadDraft(
                RowNumber: row.RowNumber(),
                Name: GetValue(row, headerMap, "name"),
                Email: GetValue(row, headerMap, "email"),
                PhoneNumber: phone,
                Website: GetValue(row, headerMap, "website"),
                Service: GetValue(row, headerMap, "service"),
                Budget: GetValue(row, headerMap, "budget")));
        }

        return Task.FromResult(new LeadSpreadsheetReadResult(leads, totalRows, skippedRows));
    }

    public Task<Stream> ExportSchedulesAsync(IEnumerable<ScheduleExportRow> rows, CancellationToken cancellationToken)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.AddWorksheet("Schedules");

        worksheet.Cell(1, 1).Value = "Lead Name";
        worksheet.Cell(1, 2).Value = "Phone Number";
        worksheet.Cell(1, 3).Value = "Agent";
        worksheet.Cell(1, 4).Value = "Time Zone";
        worksheet.Cell(1, 5).Value = "Scheduled UTC";
        worksheet.Cell(1, 6).Value = "Notes";

        var rowIndex = 2;
        foreach (var row in rows)
        {
            cancellationToken.ThrowIfCancellationRequested();

            worksheet.Cell(rowIndex, 1).Value = row.LeadName;
            worksheet.Cell(rowIndex, 2).Value = row.PhoneNumber;
            worksheet.Cell(rowIndex, 3).Value = row.AgentName;
            worksheet.Cell(rowIndex, 4).Value = row.TimeZoneId;
            worksheet.Cell(rowIndex, 5).Value = row.ScheduledForUtc.UtcDateTime;
            worksheet.Cell(rowIndex, 6).Value = row.Notes;
            rowIndex++;
        }

        worksheet.Columns().AdjustToContents();

        var output = new MemoryStream();
        workbook.SaveAs(output);
        output.Position = 0;
        return Task.FromResult<Stream>(output);
    }

    private static Dictionary<string, int> BuildHeaderMap(IXLRow? headerRow)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        if (headerRow is null)
        {
            return map;
        }

        foreach (var cell in headerRow.CellsUsed())
        {
            var key = Normalize(cell.GetString());
            if (!map.ContainsKey(key))
            {
                map[key] = cell.Address.ColumnNumber;
            }
        }

        return map;
    }

    private static void ValidateRequiredColumns(IReadOnlyDictionary<string, int> headerMap)
    {
        var missingColumns = RequiredColumns
            .Where(column => !GetAliases(column).Any(headerMap.ContainsKey))
            .ToArray();

        if (missingColumns.Length > 0)
        {
            throw new InvalidOperationException(
                $"The Excel sheet is missing required columns: {string.Join(", ", missingColumns)}.");
        }
    }

    private static string GetValue(IXLRow row, IReadOnlyDictionary<string, int> headerMap, string key)
    {
        var aliases = GetAliases(key);
        var column = aliases
            .Select(alias => headerMap.TryGetValue(alias, out var index) ? index : (int?)null)
            .FirstOrDefault(index => index.HasValue);

        return column.HasValue
            ? row.Cell(column.Value).GetString().Trim()
            : string.Empty;
    }

    private static IEnumerable<string> GetAliases(string key)
    {
        var normalized = Normalize(key);

        return normalized switch
        {
            "phone" => ["phone", "phonenumber", "phoneno", "mobile", "contactnumber"],
            _ => [normalized],
        };
    }

    private static string Normalize(string value)
    {
        return value.Trim().Replace(" ", string.Empty).Replace("_", string.Empty).ToLowerInvariant();
    }
}
