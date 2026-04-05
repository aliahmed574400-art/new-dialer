namespace NewDialer.Desktop.Models;

public sealed class AgentAdminRow
{
    public required Guid AgentId { get; init; }

    public required string FullName { get; init; }

    public required string Email { get; init; }

    public required string Username { get; init; }

    public required int CallsToday { get; init; }

    public required int ScheduledCalls { get; init; }

    public required string TalkTime { get; init; }

    public required string CheckIn { get; init; }

    public required string CheckOut { get; init; }
}
