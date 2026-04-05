using NewDialer.Contracts.Auth;
using NewDialer.Contracts.Dialer;
using NewDialer.Contracts.Leads;
using NewDialer.Contracts.Platform;
using NewDialer.Desktop.Models;
using NewDialer.Domain.Enums;

namespace NewDialer.Desktop.ViewModels;

public sealed partial class MainWindowViewModel
{
    public string DialerStatusText => _dialerStatus switch
    {
        DialerRunStatus.Idle => "Ready to dial",
        DialerRunStatus.Running => _currentLeadMarkedAnswered ? "Connected in Zoom Workplace" : "Dialing through Zoom Workplace",
        DialerRunStatus.Paused => "Paused after the current lead",
        DialerRunStatus.Stopped => "Stopped manually",
        DialerRunStatus.Completed => "Queue completed",
        _ => "Ready to dial",
    };

    public bool CanStart => CanUseDialer
        && !IsBusy
        && (_dialerStatus is DialerRunStatus.Idle or DialerRunStatus.Stopped or DialerRunStatus.Completed || (_dialerStatus == DialerRunStatus.Paused && CurrentLead is null))
        && GetPreferredLeadForStart() is not null;

    public bool CanPause => CanUseDialer && !IsBusy && _dialerStatus == DialerRunStatus.Running;

    public bool CanResume => CanUseDialer && !IsBusy && _dialerStatus == DialerRunStatus.Paused;

    public bool CanStop => CanUseDialer && !IsBusy && (_dialerStatus is DialerRunStatus.Running or DialerRunStatus.Paused || !string.IsNullOrWhiteSpace(_currentExternalCallId));

    public bool CanHangUp => CanUseDialer && !IsBusy && CurrentLead is not null && !string.IsNullOrWhiteSpace(_currentExternalCallId) && (_dialerStatus is DialerRunStatus.Running or DialerRunStatus.Paused);

    public bool CanMarkPickedUp => CanUseDialer && !IsBusy && CurrentLead is not null && !string.IsNullOrWhiteSpace(_currentExternalCallId) && !_currentLeadMarkedAnswered;

    public bool CanSkipLead => CanUseDialer && !IsBusy && CurrentLead is not null && !string.IsNullOrWhiteSpace(_currentExternalCallId);

    public bool HasDialerSessionStarted => _hasDialerSessionStarted || _dialerStatus is DialerRunStatus.Running or DialerRunStatus.Paused or DialerRunStatus.Stopped or DialerRunStatus.Completed;

    public bool HasCurrentCall => CurrentLead is not null;

    public string DialerBannerText => "Zoom Workplace runs locally on this PC. Start Dialing opens Zoom Phone, Hang Up ends the current call, and the next lead starts after 3 seconds.";

    public int TotalLeadCount => LeadQueue.Count;

    public int AnsweredLeadCount => LeadQueue.Count(x => x.QueueState == DialerLeadQueueState.Answered);

    public int NoAnswerLeadCount => LeadQueue.Count(x => x.QueueState == DialerLeadQueueState.NoAnswer);

    public int PendingLeadCount => LeadQueue.Count(x => x.QueueState is DialerLeadQueueState.Pending or DialerLeadQueueState.Scheduled);

    public double DialerProgressMaximum => Math.Max(LeadQueue.Count, 1);

    public double DialerProgressValue => CurrentLead is null
        ? Math.Min(_currentLeadIndex + 1, LeadQueue.Count)
        : _currentLeadIndex + 1;

    public string DialerProgressText => $"{Math.Max(Math.Min(_currentLeadIndex + 1, LeadQueue.Count), CurrentLead is null ? 0 : 1)} / {LeadQueue.Count}";

    public string CurrentLeadInitials => CurrentLead?.Initials ?? "ND";

    public string CurrentCallPrompt => CurrentLead is null
        ? "Press Start Dialing to begin with the next assigned lead."
        : _currentLeadMarkedAnswered
            ? "Connected in Zoom. Finish the conversation, then press Hang Up."
            : "Ringing in Zoom Workplace. If they answer, press Picked Up.";

    public string CurrentCallElapsedDisplay => TimeSpan.FromSeconds(_currentCallElapsedSeconds).ToString(@"mm\:ss");

    private async Task RefreshWorkspaceAsync()
    {
        if (!IsAuthenticated)
        {
            return;
        }

        SetBusy(true);
        ClearMessages();
        StatusMessage = "Refreshing workspace data...";

        try
        {
            await LoadWorkspaceDataAsync();
            StatusMessage = "Workspace synced.";
        }
        catch (Exception exception)
        {
            ErrorMessage = exception.Message;
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void ApplySession(SessionDto session)
    {
        _session = session;
        _apiClient.AccessToken = session.AccessToken;
        _dialerStatus = DialerRunStatus.Idle;
        _currentExternalCallId = null;
        _currentLeadIndex = -1;
        _currentCallStartedAtUtc = null;
        _currentCallElapsedSeconds = 0;
        _currentLeadMarkedAnswered = false;
        _hasDialerSessionStarted = false;
        CancelAutoAdvance();
        StopCallDurationTimer();
        _zoomWarmupStarted = false;
        CurrentLead = null;
        RefreshRoleContext();
        UpdateSubscriptionSummary();
        RebuildNavigation();
        RefreshDashboard();
        ResetAuthenticationForms();
        RaiseSessionChanged();
    }

    private async Task LoadWorkspaceDataAsync(Guid? preferredAgentId = null)
    {
        var selectedAgentId = preferredAgentId ?? SelectedAgent?.AgentId;
        LeadQueue.Clear();
        AdminAssignableLeads.Clear();
        ScheduledCalls.Clear();
        Agents.Clear();
        SelectedAgentLeads.Clear();
        AgentPerformance.Clear();
        DeveloperTenants.Clear();
        AgentOptions.Clear();
        ImportBatches.Clear();
        SelectedLead = null;
        SelectedScheduledCall = null;
        SelectedAgent = null;
        CurrentLead = null;
        _currentLeadIndex = -1;
        _currentExternalCallId = null;
        _currentCallStartedAtUtc = null;
        _currentCallElapsedSeconds = 0;
        _currentLeadMarkedAnswered = false;
        _hasDialerSessionStarted = false;
        CancelAutoAdvance();
        StopCallDurationTimer();
        _dialerStatus = DialerRunStatus.Idle;

        if (!IsAuthenticated || !CanViewData)
        {
            RefreshDashboard();
            RaiseDialerStateChanged();
            return;
        }

        if (IsDeveloper)
        {
            PopulateDeveloperOverview(await _apiClient.GetPlatformOverviewAsync(CancellationToken.None));
        }
        else
        {
            var leadTask = _apiClient.GetLeadsAsync(IsAgent, CancellationToken.None);
            var scheduleTask = _apiClient.GetScheduledCallsAsync(CancellationToken.None);
            Task<IReadOnlyList<NewDialer.Contracts.Agents.AgentAdminDto>>? agentsTask = null;
            Task<IReadOnlyList<NewDialer.Contracts.Leads.AgentAssignmentOptionDto>>? agentOptionsTask = null;
            Task<IReadOnlyList<NewDialer.Contracts.Leads.LeadImportBatchDto>>? importBatchesTask = null;

            if (IsAdmin)
            {
                agentsTask = _apiClient.GetAgentsAsync(CancellationToken.None);
                agentOptionsTask = _apiClient.GetAgentOptionsAsync(CancellationToken.None);
                importBatchesTask = _apiClient.GetImportBatchesAsync(CancellationToken.None);
            }

            var backgroundTasks = new List<Task> { leadTask, scheduleTask };
            if (agentsTask is not null)
            {
                backgroundTasks.Add(agentsTask);
            }

            if (agentOptionsTask is not null)
            {
                backgroundTasks.Add(agentOptionsTask);
            }

            if (importBatchesTask is not null)
            {
                backgroundTasks.Add(importBatchesTask);
            }

            await Task.WhenAll(backgroundTasks);
            PopulateLeadQueue(leadTask.Result);
            PopulateSchedules(scheduleTask.Result);

            if (agentsTask is not null)
            {
                PopulateAgents(agentsTask.Result, selectedAgentId);
                if (SelectedAgent is not null)
                {
                    await LoadSelectedAgentLeadsAsync(SelectedAgent.AgentId, clearMessages: false);
                }
            }

            if (agentOptionsTask is not null)
            {
                PopulateAgentOptions(agentOptionsTask.Result);
            }

            if (importBatchesTask is not null)
            {
                PopulateImportBatches(importBatchesTask.Result);
            }
        }

        RefreshDashboard();
        RaiseDialerStateChanged();
    }

    private async Task<bool> CreateScheduleCoreAsync(ScheduledCallDraft draft)
    {
        if (CurrentLead is null || !CanUseDialer)
        {
            return false;
        }

        SetBusy(true);
        ClearMessages();
        StatusMessage = $"Saving callback for {CurrentLead.Name}...";

        try
        {
            var schedule = await _apiClient.CreateScheduledCallAsync(
                new CreateScheduledCallRequest(CurrentLead.Id, draft.ScheduledForUtc, draft.TimeZoneId, draft.Notes),
                CancellationToken.None);

            ScheduledCalls.Insert(0, MapSchedule(schedule));
            CurrentLead.QueueState = DialerLeadQueueState.Scheduled;
            SelectedScheduledCall = ScheduledCalls[0];
            RefreshDashboard();
            StatusMessage = $"Callback saved for {CurrentLead.Name}.";
            return true;
        }
        catch (Exception exception)
        {
            ErrorMessage = exception.Message;
            return false;
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void PopulateLeadQueue(IEnumerable<LeadDto> leads)
    {
        LeadQueue.Clear();
        AdminAssignableLeads.Clear();
        var queueNumber = 1;
        foreach (var lead in leads)
        {
            var row = MapLeadRow(lead, useAgentStatusLabels: false, queueNumber++);
            LeadQueue.Add(row);

            if (lead.AssignedAgentId is null)
            {
                AdminAssignableLeads.Add(MapLeadRow(lead, useAgentStatusLabels: false, queueNumber: 0));
            }
        }
    }

    private void PopulateSchedules(IEnumerable<ScheduledCallListItemDto> schedules)
    {
        ScheduledCalls.Clear();
        foreach (var schedule in schedules.OrderBy(x => x.ScheduledForUtc))
        {
            ScheduledCalls.Add(MapSchedule(schedule));
        }
    }

    private void PopulateDeveloperOverview(IEnumerable<TenantOverviewDto> tenants)
    {
        DeveloperTenants.Clear();
        foreach (var tenant in tenants)
        {
            DeveloperTenants.Add(new DeveloperTenantRow
            {
                CompanyName = tenant.CompanyName,
                AdminEmail = tenant.AdminEmail,
                Plan = tenant.PlanName,
                Status = tenant.Status.ToString(),
                TrialEnds = tenant.TrialEndsAtUtc?.ToString("dd MMM yyyy") ?? "-",
                CurrentPeriodEnds = tenant.CurrentPeriodEndsAtUtc?.ToString("dd MMM yyyy") ?? "-",
            });
        }
    }

    private ScheduledCallEntry MapSchedule(ScheduledCallListItemDto schedule)
    {
        return new ScheduledCallEntry
        {
            Id = schedule.Id,
            LeadId = schedule.LeadId,
            AgentId = schedule.AgentId,
            LeadName = schedule.LeadName,
            AgentName = schedule.AgentName,
            PhoneNumber = schedule.PhoneNumber,
            TimeZoneId = schedule.TimeZoneId,
            Notes = schedule.Notes,
            ScheduledForUtc = schedule.ScheduledForUtc,
            Status = schedule.Status.ToString(),
        };
    }

    private void RefreshRoleContext()
    {
        if (_session is null)
        {
            CurrentUserName = "Sign in to NewDialer";
            RoleHeadline = "Use your email to sign in, or use an agent username with the workspace key.";
            return;
        }

        CurrentUserName = _session.FullName;
        RoleHeadline = _session.Role switch
        {
            UserRole.Admin => "Admin controls leads, scheduling, subscriptions, and agent workspace access.",
            UserRole.Agent => "Agent-safe mode with assigned leads, Zoom dialing, and callback scheduling only.",
            UserRole.Developer => "Developer oversight across tenants, trials, renewals, and manual activations.",
            _ => "Workspace access ready.",
        };
    }

    private void RebuildNavigation()
    {
        var selectedSection = SelectedNavigation?.Section ?? ShellSection.Dashboard;
        NavigationItems.Clear();
        if (!IsAuthenticated)
        {
            return;
        }

        NavigationItems.Add(new ShellNavItem { Title = "Dashboard", Subtitle = "Live operating picture", Section = ShellSection.Dashboard });
        if (!IsDeveloper)
        {
            NavigationItems.Add(new ShellNavItem { Title = "Dialer", Subtitle = "Continuous outbound calling", Section = ShellSection.Dialer });
            NavigationItems.Add(new ShellNavItem { Title = "Schedule", Subtitle = "Callback planning and follow-up", Section = ShellSection.Schedule });
        }

        if (IsAdmin)
        {
            NavigationItems.Add(new ShellNavItem { Title = "Leads", Subtitle = "Workspace lead visibility", Section = ShellSection.Leads });
            NavigationItems.Add(new ShellNavItem { Title = "Agents", Subtitle = "Analytics and staffing", Section = ShellSection.Agents });
            NavigationItems.Add(new ShellNavItem { Title = "Billing", Subtitle = "Trial and subscription management", Section = ShellSection.Billing });
        }

        if (IsDeveloper)
        {
            NavigationItems.Add(new ShellNavItem { Title = "Billing", Subtitle = "Plan state across tenants", Section = ShellSection.Billing });
            NavigationItems.Add(new ShellNavItem { Title = "Developer", Subtitle = "Global tenant oversight", Section = ShellSection.Developer });
        }

        SelectedNavigation = NavigationItems.FirstOrDefault(x => x.Section == selectedSection) ?? NavigationItems.FirstOrDefault();
    }

    private void UpdateSubscriptionSummary()
    {
        SubscriptionSummary = _session?.SubscriptionMessage ?? "Create an admin workspace or sign in to an existing tenant.";
    }

    private void RefreshDashboard()
    {
        DashboardCards.Clear();
        if (!IsAuthenticated)
        {
            DashboardCards.Add(new MetricCard { Title = "Mode", Value = "Offline", Subtitle = "Sign in to load live workspace data" });
            DashboardCards.Add(new MetricCard { Title = "API", Value = "Ready", Subtitle = "Desktop app can connect to your backend API" });
            DashboardCards.Add(new MetricCard { Title = "Security", Value = "JWT", Subtitle = "Role-aware sessions for admins, agents, and developers" });
            return;
        }

        if (IsDeveloper)
        {
            var actionNeeded = DeveloperTenants.Count(x => string.Equals(x.Status, SubscriptionStatus.PastDue.ToString(), StringComparison.OrdinalIgnoreCase) || string.Equals(x.Status, SubscriptionStatus.Expired.ToString(), StringComparison.OrdinalIgnoreCase));
            DashboardCards.Add(new MetricCard { Title = "Tenants", Value = DeveloperTenants.Count.ToString(), Subtitle = "Visible across the platform" });
            DashboardCards.Add(new MetricCard { Title = "Action Needed", Value = actionNeeded.ToString(), Subtitle = "Past due or expired subscriptions" });
            DashboardCards.Add(new MetricCard { Title = "Access", Value = AccessModeLabel, Subtitle = "Developer oversight session" });
            return;
        }

        DashboardCards.Add(new MetricCard { Title = IsAgent ? "Assigned Leads" : "Workspace Leads", Value = LeadQueue.Count.ToString(), Subtitle = "Visible in the current queue" });
        DashboardCards.Add(new MetricCard { Title = "Ready To Dial", Value = LeadQueue.Count(CanLeadBeDialed).ToString(), Subtitle = "Pending, missed, or scheduled callbacks" });
        DashboardCards.Add(new MetricCard { Title = "Scheduled", Value = ScheduledCalls.Count.ToString(), Subtitle = "Callbacks saved in PostgreSQL" });
        DashboardCards.Add(new MetricCard { Title = "Access", Value = AccessModeLabel, Subtitle = SubscriptionSummary });
    }

    private void ResetSession()
    {
        _session = null;
        _apiClient.AccessToken = null;
        ResetAuthenticationForms();
        SelectedImportAgent = null;
        SelectedAssignmentAgent = null;
        ImportNotes = string.Empty;
        LeadQueue.Clear();
        AdminAssignableLeads.Clear();
        ScheduledCalls.Clear();
        Agents.Clear();
        SelectedAgentLeads.Clear();
        AgentPerformance.Clear();
        DeveloperTenants.Clear();
        AgentOptions.Clear();
        ImportBatches.Clear();
        SelectedLead = null;
        SelectedScheduledCall = null;
        ClearAgentForm();
        CurrentLead = null;
        _currentLeadIndex = -1;
        _currentExternalCallId = null;
        _currentCallStartedAtUtc = null;
        _currentCallElapsedSeconds = 0;
        _currentLeadMarkedAnswered = false;
        _hasDialerSessionStarted = false;
        CancelAutoAdvance();
        StopCallDurationTimer();
        _dialerStatus = DialerRunStatus.Idle;
        _zoomWarmupStarted = false;
        RefreshRoleContext();
        UpdateSubscriptionSummary();
        RebuildNavigation();
        RefreshDashboard();
        RaiseSessionChanged();
    }

    private void RaiseSessionChanged()
    {
        OnPropertyChanged(nameof(IsAuthenticated));
        OnPropertyChanged(nameof(TenantName));
        OnPropertyChanged(nameof(WorkspaceKeyDisplay));
        OnPropertyChanged(nameof(AccessModeLabel));
        OnPropertyChanged(nameof(RoleLabel));
        OnPropertyChanged(nameof(IsDeveloper));
        OnPropertyChanged(nameof(IsAdmin));
        OnPropertyChanged(nameof(IsAgent));
        OnPropertyChanged(nameof(CanUseDialer));
        OnPropertyChanged(nameof(CanViewData));
        OnPropertyChanged(nameof(CanManageLeads));
        OnPropertyChanged(nameof(CanManageAgents));
        OnPropertyChanged(nameof(AgentAnalyticsMessage));
        RaiseDialerStateChanged();
    }

    private void WarmUpZoomDesktopIfNeeded()
    {
        if (_zoomWarmupStarted || !CanUseDialer || SelectedSection != ShellSection.Dialer)
        {
            return;
        }

        _zoomWarmupStarted = true;
        _ = WarmUpZoomDesktopCoreAsync();
    }

    private async Task WarmUpZoomDesktopCoreAsync()
    {
        try
        {
            await _zoomDesktopDialerClient.WarmUpAsync(CancellationToken.None);
        }
        catch
        {
            _zoomWarmupStarted = false;
        }
    }

    private static string MapLeadStatus(LeadStatus status)
    {
        return status switch
        {
            LeadStatus.New => "Pending",
            LeadStatus.Queued => "Pending",
            LeadStatus.Dialing => "Calling",
            LeadStatus.Completed => "Answered",
            LeadStatus.FollowUpScheduled => "Scheduled",
            LeadStatus.DoNotCall => "Do Not Call",
            LeadStatus.Failed => "No Answer",
            _ => "Pending",
        };
    }

    private static string MapAgentSheetStatus(LeadStatus status)
    {
        return status switch
        {
            LeadStatus.Completed => "Dialed",
            LeadStatus.Dialing => "Calling",
            LeadStatus.FollowUpScheduled => "Scheduled",
            LeadStatus.Failed => "Missed",
            LeadStatus.DoNotCall => "Do Not Call",
            LeadStatus.New => "New",
            LeadStatus.Queued => "Queued",
            _ => "Queued",
        };
    }

    private static DialerLeadRow MapLeadRow(LeadDto lead, bool useAgentStatusLabels, int queueNumber)
    {
        return new DialerLeadRow
        {
            Id = lead.Id,
            QueueNumber = queueNumber,
            Name = lead.Name,
            Email = lead.Email,
            PhoneNumber = lead.PhoneNumber,
            Website = lead.Website,
            Service = lead.Service,
            Budget = lead.Budget,
            AssignedAgentName = lead.AssignedAgentName,
            QueueState = MapLeadQueueState(lead.Status),
            StatusLabelOverride = useAgentStatusLabels ? MapAgentSheetStatus(lead.Status) : null,
        };
    }

    private static bool CanLeadBeDialed(DialerLeadRow lead)
    {
        return lead.QueueState is DialerLeadQueueState.Pending or DialerLeadQueueState.Scheduled or DialerLeadQueueState.NoAnswer;
    }

    private static DialerLeadQueueState MapLeadQueueState(LeadStatus status)
    {
        return status switch
        {
            LeadStatus.New => DialerLeadQueueState.Pending,
            LeadStatus.Queued => DialerLeadQueueState.Pending,
            LeadStatus.Dialing => DialerLeadQueueState.Calling,
            LeadStatus.Completed => DialerLeadQueueState.Answered,
            LeadStatus.FollowUpScheduled => DialerLeadQueueState.Scheduled,
            LeadStatus.DoNotCall => DialerLeadQueueState.DoNotCall,
            LeadStatus.Failed => DialerLeadQueueState.NoAnswer,
            _ => DialerLeadQueueState.Pending,
        };
    }

    private void StopCallDurationTimer()
    {
        _callDurationTimer.Stop();
        _currentCallStartedAtUtc = null;
        _currentCallElapsedSeconds = 0;
        OnPropertyChanged(nameof(CurrentCallElapsedDisplay));
    }

    private void StartCallDurationTimer()
    {
        _currentCallStartedAtUtc = DateTimeOffset.UtcNow;
        _currentCallElapsedSeconds = 0;
        OnPropertyChanged(nameof(CurrentCallElapsedDisplay));
        _callDurationTimer.Start();
    }

    private void CancelAutoAdvance()
    {
        _autoAdvanceCancellationSource?.Cancel();
        _autoAdvanceCancellationSource?.Dispose();
        _autoAdvanceCancellationSource = null;
    }
}
