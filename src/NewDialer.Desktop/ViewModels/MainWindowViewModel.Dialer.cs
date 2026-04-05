using NewDialer.Contracts.Dialer;
using NewDialer.Desktop.Models;
using NewDialer.Domain.Enums;

namespace NewDialer.Desktop.ViewModels;

public sealed partial class MainWindowViewModel
{
    private DialerLeadRow? GetPreferredLeadForStart()
    {
        return SelectedLead is not null && CanLeadBeDialed(SelectedLead)
            ? SelectedLead
            : GetNextCallableLead();
    }

    private DialerLeadRow? GetNextCallableLead()
    {
        if (_currentLeadIndex < 0)
        {
            return LeadQueue.FirstOrDefault(CanLeadBeDialed);
        }

        return LeadQueue.Skip(_currentLeadIndex + 1).FirstOrDefault(CanLeadBeDialed)
            ?? LeadQueue.FirstOrDefault(CanLeadBeDialed);
    }

    private void SetCurrentLead(DialerLeadRow nextLead)
    {
        foreach (var lead in LeadQueue.Where(x => x.Status == "Dialing"))
        {
            lead.Status = "Queued";
        }

        nextLead.Status = "Dialing";
        CurrentLead = nextLead;
        SelectedLead = nextLead;
        _currentLeadIndex = LeadQueue.IndexOf(nextLead);
        RefreshDashboard();
    }

    private async Task StartDialerAsync()
    {
        await StartLeadAsync(GetPreferredLeadForStart());
    }

    private void PauseDialer()
    {
        _dialerStatus = DialerRunStatus.Paused;
        StatusMessage = CurrentLead is null ? "Dialer paused." : "Dialer paused. The next lead will wait until resume.";
        RaiseDialerStateChanged();
    }

    private async Task ResumeDialerAsync()
    {
        if (CurrentLead is not null)
        {
            _dialerStatus = DialerRunStatus.Running;
            StatusMessage = $"Dialer resumed for {CurrentLead.Name}.";
            RaiseDialerStateChanged();
            return;
        }

        _dialerStatus = DialerRunStatus.Running;
        RaiseDialerStateChanged();
        await StartLeadAsync(GetNextCallableLead());
    }

    private async Task StopDialerAsync()
    {
        SetBusy(true);
        ClearMessages();
        StatusMessage = "Stopping dialer...";

        try
        {
            if (!string.IsNullOrWhiteSpace(_currentExternalCallId))
            {
                await SafeHangUpAsync();
            }

            if (CurrentLead is not null && CurrentLead.Status == "Dialing")
            {
                CurrentLead.Status = "Completed";
            }

            CurrentLead = null;
            _currentLeadIndex = -1;
            _dialerStatus = DialerRunStatus.Stopped;
            StatusMessage = "Dialer stopped.";
            RaiseDialerStateChanged();
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task HangUpAndContinueAsync()
    {
        if (CurrentLead is null || string.IsNullOrWhiteSpace(_currentExternalCallId))
        {
            return;
        }

        var finishedLead = CurrentLead;
        var wasPaused = _dialerStatus == DialerRunStatus.Paused;
        DialerLeadRow? nextLead = null;

        SetBusy(true);
        ClearMessages();
        StatusMessage = $"Ending call for {finishedLead.Name}...";

        try
        {
            await SafeHangUpAsync();
            finishedLead.Status = "Completed";
            CurrentLead = null;
            RefreshDashboard();

            if (wasPaused)
            {
                _dialerStatus = DialerRunStatus.Paused;
                StatusMessage = $"Call ended for {finishedLead.Name}.";
            }
            else
            {
                nextLead = GetNextCallableLead();
                _dialerStatus = nextLead is null ? DialerRunStatus.Completed : DialerRunStatus.Running;
                StatusMessage = nextLead is null ? $"Call ended for {finishedLead.Name}. Queue completed." : StatusMessage;
            }

            RaiseDialerStateChanged();
        }
        finally
        {
            SetBusy(false);
        }

        if (!wasPaused && nextLead is not null)
        {
            await StartLeadAsync(nextLead);
        }
    }

    private async Task StartScheduledCallAsync()
    {
        if (SelectedScheduledCall is null)
        {
            return;
        }

        var lead = LeadQueue.FirstOrDefault(x => x.Id == SelectedScheduledCall.LeadId);
        if (lead is null)
        {
            ErrorMessage = "The selected scheduled lead is not available in the current queue.";
            return;
        }

        SelectedLead = lead;
        SelectedNavigation = NavigationItems.FirstOrDefault(x => x.Section == ShellSection.Dialer) ?? SelectedNavigation;
        await StartLeadAsync(lead);
    }

    private async Task StartLeadAsync(DialerLeadRow? lead)
    {
        if (lead is null)
        {
            _dialerStatus = DialerRunStatus.Completed;
            StatusMessage = "No callable leads are available in the current queue.";
            RaiseDialerStateChanged();
            return;
        }

        SetBusy(true);
        ClearMessages();
        StatusMessage = $"Dialing {lead.Name}...";

        try
        {
            var localExternalCallId = $"desktop-{Guid.NewGuid():N}";
            await _zoomDesktopDialerClient.StartCallAsync(lead.PhoneNumber, CancellationToken.None);
            SetCurrentLead(lead);
            _currentExternalCallId = localExternalCallId;
            _dialerStatus = DialerRunStatus.Running;

            try
            {
                var response = await _apiClient.StartCallAsync(
                    new StartCallRequest(lead.Id, lead.PhoneNumber, lead.Name, localExternalCallId),
                    CancellationToken.None);

                if (!string.IsNullOrWhiteSpace(response.ExternalCallId))
                {
                    _currentExternalCallId = response.ExternalCallId;
                }

                StatusMessage = string.IsNullOrWhiteSpace(response.Message)
                    ? $"Zoom desktop dial request sent for {lead.Name}."
                    : response.Message;
            }
            catch (Exception exception)
            {
                ErrorMessage = exception.Message;
                StatusMessage = $"Zoom desktop dial request sent for {lead.Name}, but backend call logging needs attention.";
            }

            RaiseDialerStateChanged();
        }
        catch (Exception exception)
        {
            lead.Status = "Failed";
            _dialerStatus = DialerRunStatus.Stopped;
            ErrorMessage = exception.Message;
            RaiseDialerStateChanged();
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task SafeHangUpAsync()
    {
        if (string.IsNullOrWhiteSpace(_currentExternalCallId))
        {
            return;
        }

        var externalCallId = _currentExternalCallId;
        await _zoomDesktopDialerClient.HangUpAsync(CancellationToken.None);
        _currentExternalCallId = null;

        try
        {
            await _apiClient.HangUpAsync(new HangUpCallRequest(externalCallId), CancellationToken.None);
        }
        catch (Exception exception)
        {
            ErrorMessage = exception.Message;
            StatusMessage = "Zoom desktop ended the call, but backend call logging needs attention.";
        }
    }
}
