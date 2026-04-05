using NewDialer.Desktop.ViewModels;

namespace NewDialer.Desktop.Models;

public sealed class DialerLeadRow : ViewModelBase
{
    private DialerLeadQueueState _queueState = DialerLeadQueueState.Pending;
    private string? _statusLabelOverride;
    private bool _isCurrent;
    private int _queueNumber;

    public Guid Id { get; init; }

    public required string Name { get; init; }

    public required string Email { get; init; }

    public required string PhoneNumber { get; init; }

    public required string Website { get; init; }

    public required string Service { get; init; }

    public required string Budget { get; init; }

    public string? AssignedAgentName { get; init; }

    public int QueueNumber
    {
        get => _queueNumber;
        set => SetProperty(ref _queueNumber, value);
    }

    public DialerLeadQueueState QueueState
    {
        get => _queueState;
        set
        {
            if (SetProperty(ref _queueState, value))
            {
                OnPropertyChanged(nameof(Status));
                OnPropertyChanged(nameof(IsDialable));
            }
        }
    }

    public string? StatusLabelOverride
    {
        get => _statusLabelOverride;
        set
        {
            if (SetProperty(ref _statusLabelOverride, value))
            {
                OnPropertyChanged(nameof(Status));
            }
        }
    }

    public bool IsCurrent
    {
        get => _isCurrent;
        set => SetProperty(ref _isCurrent, value);
    }

    public string Status => StatusLabelOverride ?? QueueState switch
    {
        DialerLeadQueueState.Pending => "Pending",
        DialerLeadQueueState.Calling => "Calling",
        DialerLeadQueueState.Answered => "Answered",
        DialerLeadQueueState.NoAnswer => "No Answer",
        DialerLeadQueueState.Scheduled => "Scheduled",
        DialerLeadQueueState.DoNotCall => "Do Not Call",
        _ => "Pending",
    };

    public bool IsDialable => QueueState is DialerLeadQueueState.Pending or DialerLeadQueueState.Scheduled or DialerLeadQueueState.NoAnswer;

    public string Initials
    {
        get
        {
            var parts = Name
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Take(2)
                .Select(x => char.ToUpperInvariant(x[0]))
                .ToArray();

            return parts.Length == 0 ? "--" : new string(parts);
        }
    }

    public void SetQueueNumber(int queueNumber)
    {
        QueueNumber = queueNumber;
    }
}
