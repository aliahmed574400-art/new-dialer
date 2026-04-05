using System.Windows.Threading;
using System.Collections.ObjectModel;
using System.Windows.Input;
using NewDialer.Contracts.Agents;
using NewDialer.Contracts.Auth;
using NewDialer.Contracts.Leads;
using NewDialer.Desktop.Commands;
using NewDialer.Desktop.Models;
using NewDialer.Desktop.Services;
using NewDialer.Domain.Enums;

namespace NewDialer.Desktop.ViewModels;

public sealed partial class MainWindowViewModel : ViewModelBase
{
    private readonly NewDialerApiClient _apiClient;
    private readonly ZoomDesktopDialerClient _zoomDesktopDialerClient;
    private readonly AsyncRelayCommand _authenticateCommand;
    private readonly AsyncRelayCommand _refreshWorkspaceCommand;
    private readonly AsyncRelayCommand _startDialerCommand;
    private readonly AsyncRelayCommand _resumeDialerCommand;
    private readonly AsyncRelayCommand _stopDialerCommand;
    private readonly AsyncRelayCommand _hangUpCommand;
    private readonly AsyncRelayCommand _skipLeadCommand;
    private readonly AsyncRelayCommand _startScheduledCallCommand;
    private readonly AsyncRelayCommand _logoutCommand;
    private readonly RelayCommand _markPickedUpCommand;
    private readonly RelayCommand _pauseDialerCommand;
    private readonly RelayCommand _toggleAuthenticationModeCommand;
    private readonly DispatcherTimer _callDurationTimer = new() { Interval = TimeSpan.FromSeconds(1) };

    private SessionDto? _session;
    private ShellNavItem? _selectedNavigation;
    private AgentAdminRow? _selectedAgent;
    private DialerLeadRow? _currentLead;
    private DialerLeadRow? _selectedLead;
    private ScheduledCallEntry? _selectedScheduledCall;
    private DialerRunStatus _dialerStatus = DialerRunStatus.Idle;
    private string _apiBaseUrl;
    private string _currentUserName = "Sign in to NewDialer";
    private string _roleHeadline = "Use your admin email or your agent username with the workspace key.";
    private string _subscriptionSummary = "Create an admin workspace or sign in to an existing tenant.";
    private string _statusMessage = "Desktop app ready for secure workspace sign-in.";
    private string _errorMessage = string.Empty;
    private int _currentLeadIndex = -1;
    private int _currentCallElapsedSeconds;
    private string? _currentExternalCallId;
    private DateTimeOffset? _currentCallStartedAtUtc;
    private CancellationTokenSource? _autoAdvanceCancellationSource;
    private bool _isBusy;
    private bool _hasDialerSessionStarted;
    private bool _currentLeadMarkedAnswered;
    private bool _zoomWarmupStarted;

    public MainWindowViewModel(NewDialerApiClient apiClient, ZoomDesktopDialerClient zoomDesktopDialerClient)
    {
        _apiClient = apiClient;
        _zoomDesktopDialerClient = zoomDesktopDialerClient;
        _apiBaseUrl = apiClient.ApiBaseUrl;

        DashboardCards = [];
        NavigationItems = [];
        LeadQueue = [];
        AdminAssignableLeads = [];
        ScheduledCalls = [];
        Agents = [];
        SelectedAgentLeads = [];
        AgentPerformance = [];
        DeveloperTenants = [];
        AgentOptions = [];
        ImportBatches = [];

        _authenticateCommand = new AsyncRelayCommand(AuthenticateAsync, () => CanAuthenticate);
        _refreshWorkspaceCommand = new AsyncRelayCommand(RefreshWorkspaceAsync, () => IsAuthenticated && !IsBusy);
        _startDialerCommand = new AsyncRelayCommand(StartDialerAsync, () => CanStart);
        _resumeDialerCommand = new AsyncRelayCommand(ResumeDialerAsync, () => CanResume);
        _stopDialerCommand = new AsyncRelayCommand(StopDialerAsync, () => CanStop);
        _hangUpCommand = new AsyncRelayCommand(HangUpAndContinueAsync, () => CanHangUp);
        _skipLeadCommand = new AsyncRelayCommand(SkipCurrentLeadAsync, () => CanSkipLead);
        _startScheduledCallCommand = new AsyncRelayCommand(StartScheduledCallAsync, () => CanStartScheduledCall);
        _logoutCommand = new AsyncRelayCommand(SignOutAsync, () => IsAuthenticated && !IsBusy);
        _markPickedUpCommand = new RelayCommand(MarkLeadPickedUp, () => CanMarkPickedUp);
        _pauseDialerCommand = new RelayCommand(PauseDialer, () => CanPause);
        _toggleAuthenticationModeCommand = new RelayCommand(ToggleAuthenticationMode, () => !IsBusy);
        _callDurationTimer.Tick += OnCallDurationTimerTick;

        RebuildNavigation();
        RefreshDashboard();
        RaiseCommandStates();
    }

    public ObservableCollection<MetricCard> DashboardCards { get; }

    public ObservableCollection<ShellNavItem> NavigationItems { get; }

    public ObservableCollection<DialerLeadRow> LeadQueue { get; }

    public ObservableCollection<DialerLeadRow> AdminAssignableLeads { get; }

    public ObservableCollection<ScheduledCallEntry> ScheduledCalls { get; }

    public ObservableCollection<AgentAdminRow> Agents { get; }

    public ObservableCollection<DialerLeadRow> SelectedAgentLeads { get; }

    public ObservableCollection<AgentPerformanceRow> AgentPerformance { get; }

    public ObservableCollection<DeveloperTenantRow> DeveloperTenants { get; }

    public ObservableCollection<AgentAssignmentOptionDto> AgentOptions { get; }

    public ObservableCollection<LeadImportBatchDto> ImportBatches { get; }

    public ICommand AuthenticateCommand => _authenticateCommand;

    public ICommand RefreshWorkspaceCommand => _refreshWorkspaceCommand;

    public ICommand StartDialerCommand => _startDialerCommand;

    public ICommand PauseDialerCommand => _pauseDialerCommand;

    public ICommand ResumeDialerCommand => _resumeDialerCommand;

    public ICommand StopDialerCommand => _stopDialerCommand;

    public ICommand HangUpCommand => _hangUpCommand;

    public ICommand SkipLeadCommand => _skipLeadCommand;

    public ICommand MarkPickedUpCommand => _markPickedUpCommand;

    public ICommand StartScheduledCallCommand => _startScheduledCallCommand;

    public ICommand SignOutCommand => _logoutCommand;

    public ICommand ToggleAuthenticationModeCommand => _toggleAuthenticationModeCommand;

    public ShellNavItem? SelectedNavigation
    {
        get => _selectedNavigation;
        set
        {
            if (SetProperty(ref _selectedNavigation, value))
            {
                OnPropertyChanged(nameof(SelectedSection));
                WarmUpZoomDesktopIfNeeded();
            }
        }
    }

    public ShellSection SelectedSection => SelectedNavigation?.Section ?? ShellSection.Dashboard;

    public DialerLeadRow? SelectedLead
    {
        get => _selectedLead;
        set
        {
            if (SetProperty(ref _selectedLead, value))
            {
                RaiseCommandStates();
            }
        }
    }

    public ScheduledCallEntry? SelectedScheduledCall
    {
        get => _selectedScheduledCall;
        set
        {
            if (SetProperty(ref _selectedScheduledCall, value))
            {
                RaiseCommandStates();
            }
        }
    }

    public AgentAdminRow? SelectedAgent
    {
        get => _selectedAgent;
        set
        {
            if (SetProperty(ref _selectedAgent, value))
            {
                ApplyAgentSelection(value);
            }
        }
    }

    public DialerLeadRow? CurrentLead
    {
        get => _currentLead;
        private set
        {
            if (SetProperty(ref _currentLead, value))
            {
                OnPropertyChanged(nameof(CurrentLeadName));
                OnPropertyChanged(nameof(CurrentLeadEmail));
                OnPropertyChanged(nameof(CurrentLeadPhoneNumber));
                OnPropertyChanged(nameof(CurrentLeadWebsite));
                OnPropertyChanged(nameof(CurrentLeadService));
                OnPropertyChanged(nameof(CurrentLeadBudget));
                RaiseDialerStateChanged();
            }
        }
    }

    public string CurrentUserName
    {
        get => _currentUserName;
        private set => SetProperty(ref _currentUserName, value);
    }

    public string RoleHeadline
    {
        get => _roleHeadline;
        private set => SetProperty(ref _roleHeadline, value);
    }

    public string SubscriptionSummary
    {
        get => _subscriptionSummary;
        private set => SetProperty(ref _subscriptionSummary, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set
        {
            if (SetProperty(ref _statusMessage, value))
            {
                OnPropertyChanged(nameof(HasStatusMessage));
            }
        }
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            if (SetProperty(ref _errorMessage, value))
            {
                OnPropertyChanged(nameof(HasErrorMessage));
            }
        }
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                RaiseCommandStates();
            }
        }
    }

    public bool IsAuthenticated => _session is not null;

    public bool HasStatusMessage => !string.IsNullOrWhiteSpace(StatusMessage);

    public bool HasErrorMessage => !string.IsNullOrWhiteSpace(ErrorMessage);

    public string TenantName => _session?.CompanyName ?? "NewDialer";

    public string ProductTagline => "Professional outbound dialing with subscriptions, scheduling, and agent governance.";

    public string WorkspaceKeyDisplay => _session?.WorkspaceKey ?? "Not signed in";

    public string AccessModeLabel => _session?.CanUseDialer == true ? "Dialer enabled" : "Read-only";

    public UserRole CurrentRole => _session?.Role ?? UserRole.Admin;

    public string RoleLabel => CurrentRole.ToString();

    public bool IsDeveloper => _session?.Role == UserRole.Developer;

    public bool IsAdmin => _session?.Role == UserRole.Admin;

    public bool IsAgent => _session?.Role == UserRole.Agent;

    public bool CanUseDialer => IsAuthenticated && _session?.CanUseDialer == true;

    public bool CanViewData => IsAuthenticated && _session?.CanViewData == true;

    public bool CanCreateSchedule => CanUseDialer && CurrentLead is not null && !IsBusy;

    public bool CanStartScheduledCall => CanUseDialer && SelectedScheduledCall is not null && !IsBusy && _dialerStatus != DialerRunStatus.Running;

    public string QueueSummary => IsAuthenticated
        ? $"{AnsweredLeadCount + NoAnswerLeadCount} completed / {LeadQueue.Count} loaded"
        : "Sign in to load workspace data";

    public string CurrentLeadName => CurrentLead?.Name ?? "Queue ready";

    public string CurrentLeadEmail => CurrentLead?.Email ?? "No active lead";

    public string CurrentLeadPhoneNumber => CurrentLead?.PhoneNumber ?? "Press Start Dialer to begin";

    public string CurrentLeadWebsite => CurrentLead?.Website ?? "Waiting for the first dial";

    public string CurrentLeadService => CurrentLead?.Service ?? "Lead data appears here";

    public string CurrentLeadBudget => CurrentLead?.Budget ?? "Budget appears here";

    public string AgentAnalyticsMessage => IsAdmin
        ? "Create agents, update their access details, and review live calls, schedules, and attendance from one admin panel."
        : "Agent analytics are available to admins only.";

    public async Task<bool> CreateScheduleAsync(ScheduledCallDraft draft)
    {
        return await CreateScheduleCoreAsync(draft);
    }

    private void ClearMessages()
    {
        ErrorMessage = string.Empty;
        StatusMessage = string.Empty;
    }

    private void SetBusy(bool isBusy)
    {
        IsBusy = isBusy;
        OnPropertyChanged(nameof(CanAuthenticate));
        OnPropertyChanged(nameof(CanCreateSchedule));
        OnPropertyChanged(nameof(CanUseDialer));
        OnPropertyChanged(nameof(CanManageLeads));
        OnPropertyChanged(nameof(CanManageAgents));
        OnPropertyChanged(nameof(CanSaveAgent));
        OnPropertyChanged(nameof(CanDeleteSelectedAgent));
    }

    private void RaiseDialerStateChanged()
    {
        OnPropertyChanged(nameof(DialerStatusText));
        OnPropertyChanged(nameof(CanStart));
        OnPropertyChanged(nameof(CanPause));
        OnPropertyChanged(nameof(CanResume));
        OnPropertyChanged(nameof(CanStop));
        OnPropertyChanged(nameof(CanHangUp));
        OnPropertyChanged(nameof(CanMarkPickedUp));
        OnPropertyChanged(nameof(CanSkipLead));
        OnPropertyChanged(nameof(CanStartScheduledCall));
        OnPropertyChanged(nameof(CanCreateSchedule));
        OnPropertyChanged(nameof(QueueSummary));
        OnPropertyChanged(nameof(HasDialerSessionStarted));
        OnPropertyChanged(nameof(HasCurrentCall));
        OnPropertyChanged(nameof(DialerBannerText));
        OnPropertyChanged(nameof(TotalLeadCount));
        OnPropertyChanged(nameof(AnsweredLeadCount));
        OnPropertyChanged(nameof(NoAnswerLeadCount));
        OnPropertyChanged(nameof(PendingLeadCount));
        OnPropertyChanged(nameof(DialerProgressMaximum));
        OnPropertyChanged(nameof(DialerProgressValue));
        OnPropertyChanged(nameof(DialerProgressText));
        OnPropertyChanged(nameof(CurrentLeadInitials));
        OnPropertyChanged(nameof(CurrentCallPrompt));
        OnPropertyChanged(nameof(CurrentCallElapsedDisplay));
        RaiseCommandStates();
    }

    private void RaiseCommandStates()
    {
        _authenticateCommand.RaiseCanExecuteChanged();
        _refreshWorkspaceCommand.RaiseCanExecuteChanged();
        _startDialerCommand.RaiseCanExecuteChanged();
        _pauseDialerCommand.RaiseCanExecuteChanged();
        _resumeDialerCommand.RaiseCanExecuteChanged();
        _stopDialerCommand.RaiseCanExecuteChanged();
        _hangUpCommand.RaiseCanExecuteChanged();
        _startScheduledCallCommand.RaiseCanExecuteChanged();
        _logoutCommand.RaiseCanExecuteChanged();
        _skipLeadCommand.RaiseCanExecuteChanged();
        _markPickedUpCommand.RaiseCanExecuteChanged();
        _toggleAuthenticationModeCommand.RaiseCanExecuteChanged();
    }

    private void OnCallDurationTimerTick(object? sender, EventArgs e)
    {
        if (_currentCallStartedAtUtc is null || CurrentLead is null)
        {
            return;
        }

        _currentCallElapsedSeconds = Math.Max(0, (int)(DateTimeOffset.UtcNow - _currentCallStartedAtUtc.Value).TotalSeconds);
        OnPropertyChanged(nameof(CurrentCallElapsedDisplay));
    }
}
