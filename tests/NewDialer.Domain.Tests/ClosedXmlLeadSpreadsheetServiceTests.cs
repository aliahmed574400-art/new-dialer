using ClosedXML.Excel;
using NewDialer.Infrastructure.Spreadsheets;

namespace NewDialer.Domain.Tests;

public sealed class ClosedXmlLeadSpreadsheetServiceTests
{
    private readonly ClosedXmlLeadSpreadsheetService _sut = new();

    [Fact]
    public async Task ReadLeadsAsync_accepts_phone_number_alias()
    {
        using var stream = BuildWorkbook(
            ["Name", "Email", "Phone No", "Website", "Service", "Budget"],
            ["Atlas Growth", "hello@atlas.test", "+1 415 555 0001", "atlas.test", "SEO", "$2500"]);

        var result = await _sut.ReadLeadsAsync(stream, CancellationToken.None);

        Assert.Equal(1, result.TotalRows);
        Assert.Equal(0, result.SkippedRows);
        var lead = Assert.Single(result.Leads);
        Assert.Equal("Atlas Growth", lead.Name);
        Assert.Equal("+1 415 555 0001", lead.PhoneNumber);
    }

    [Fact]
    public async Task ReadLeadsAsync_allows_missing_optional_columns_and_leaves_them_blank()
    {
        using var stream = BuildWorkbook(
            ["Phone No", "Email"],
            ["+1 415 555 0001", "hello@atlas.test"]);

        var result = await _sut.ReadLeadsAsync(stream, CancellationToken.None);

        var lead = Assert.Single(result.Leads);
        Assert.Equal(string.Empty, lead.Name);
        Assert.Equal("hello@atlas.test", lead.Email);
        Assert.Equal(string.Empty, lead.Budget);
        Assert.Equal("+1 415 555 0001", lead.PhoneNumber);
    }

    [Fact]
    public async Task ReadLeadsAsync_matches_known_aliases_in_any_order()
    {
        using var stream = BuildWorkbook(
            ["Amount", "URL", "Phone No", "Full Name", "Services"],
            ["$2500", "atlas.test", "+1 415 555 0001", "Atlas Growth", "SEO"]);

        var result = await _sut.ReadLeadsAsync(stream, CancellationToken.None);

        var lead = Assert.Single(result.Leads);
        Assert.Equal("Atlas Growth", lead.Name);
        Assert.Equal("atlas.test", lead.Website);
        Assert.Equal("SEO", lead.Service);
        Assert.Equal("$2500", lead.Budget);
    }

    [Fact]
    public async Task ReadLeadsAsync_throws_when_phone_column_missing()
    {
        using var stream = BuildWorkbook(
            ["Name", "Email", "Website", "Service", "Budget"],
            ["Atlas Growth", "hello@atlas.test", "atlas.test", "SEO", "$2500"]);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.ReadLeadsAsync(stream, CancellationToken.None));

        Assert.Contains("phone", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static MemoryStream BuildWorkbook(string[] headers, params string[] values)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.AddWorksheet("Leads");

        for (var index = 0; index < headers.Length; index++)
        {
            worksheet.Cell(1, index + 1).Value = headers[index];
        }

        for (var index = 0; index < values.Length; index++)
        {
            worksheet.Cell(2, index + 1).Value = values[index];
        }

        var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;
        return stream;
    }
}
