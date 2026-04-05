using NewDialer.Desktop.ViewModels;

namespace NewDialer.Desktop.Models;

public sealed class DialerLeadRow : ViewModelBase
{
    private string _status = "Queued";

    public Guid Id { get; init; }

    public required string Name { get; init; }

    public required string Email { get; init; }

    public required string PhoneNumber { get; init; }

    public required string Website { get; init; }

    public required string Service { get; init; }

    public required string Budget { get; init; }

    public string? AssignedAgentName { get; init; }

    public string Status
    {
        get => _status;
        set => SetProperty(ref _status, value);
    }
}
