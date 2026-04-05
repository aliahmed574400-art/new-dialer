namespace NewDialer.Desktop.Models;

public sealed class AgentPerformanceRow
{
    public required string AgentName { get; init; }

    public required int CallsToday { get; init; }

    public required string TalkTime { get; init; }

    public required string CheckIn { get; init; }

    public required string CheckOut { get; init; }
}
