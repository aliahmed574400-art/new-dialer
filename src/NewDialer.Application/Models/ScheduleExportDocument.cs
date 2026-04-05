namespace NewDialer.Application.Models;

public sealed record ScheduleExportDocument(
    string FileName,
    string ContentType,
    Stream Content);
