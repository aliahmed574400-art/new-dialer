namespace NewDialer.Desktop.Models;

public sealed class ShellNavItem
{
    public required string Title { get; init; }

    public required string Subtitle { get; init; }

    public required ShellSection Section { get; init; }
}
