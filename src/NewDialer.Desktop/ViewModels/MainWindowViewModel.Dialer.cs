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

    private DialerLeadRow? GetNextCallableLead(Guid? excludedLeadId = null)
    {
        if (_currentLeadIndex < 0)
        {
            return LeadQueue.FirstOrDefault(x => CanLeadBeDialed(x) && x.Id != excludedLeadId);
        }

        return LeadQueue.Skip(_currentLeadIndex + 1).FirstOrDefault(x => CanLeadBeDialed(x) && x.Id != excludedLeadId)
            ?? LeadQueue.Take(_currentLeadIndex + 1).FirstOrDefault(x => CanLeadBeDialed(x) && x.Id != excludedLeadId);
    }

    private void SetCurrentLead(DialerLeadRow nextLead)
    {
        foreach (var lead in LeadQueue.Where(x => x.IsCurrent))
        {
            lead.IsCurrent = false;
            if (lead.QueueState == DialerLeadQueueState.Calling)
            {
                lead.QueueState = DialerLeadQueueState.Pending;
            }
        }

        _currentLeadMarkedAnswered = false;
        nextLead.StatusLabelOverride = null;
        nextLead.QueueState = DialerLeadQueueState.Calling;
        nextLead.IsCurrent = true;
        CurrentLead = nextLead;
        SelectedLead = nextLead;
        _currentLeadIndex = LeadQueue.IndexOf(nextLead);
        _hasDialerSessionStarted = true;
        StartCallDurationTimer();
        RefreshDashboard();
        RaiseDialerStateChanged();
    }

    private async Task StartDialerAsync()
    {
        _hasDialerSessionStarted = true;
        RaiseDialerStateChanged();
        await StartLeadAsync(GetPreferredLeadForStart());
    }

    private void PauseDialer()
    {
        _dialerStatus = DialerRunStatus.Paused;
        StatusMessage = CurrentLead is null
            ? "Dialer paused."
            : "Dialer paused. Finish the current lead, then resume when you are ready.";
        RaiseDialerStateChanged();
    }

    private async Task ResumeDialerAsync()
    {
        CancelAutoAdvance();

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
        CancelAutoAdvance();
        SetBusy(true);
        ClearMessages();
        StatusMessage = "Stopping dialer...";

        try
        {
            if (CurrentLead is not null && !string.IsNullOrWhiteSpace(_currentExternalCallId))
            {
                var preserveAnsweredState = _currentLeadMarkedAnswered;
                await SafeHangUpAsync(
                    wasAnswered: preserveAnsweredState,
                    requeueLead: !preserveAnsweredState,
                    outcomeLabel: preserveAnsweredState ? "Answered" : "Stopped");

                if (preserveAnsweredState)
                {
                    CurrentLead.QueueState = DialerLeadQueueState.Answered;
                }
                else
                {
                    RequeueLead(CurrentLead);
                }

                CurrentLead.IsCurrent = false;
            }

            StopCallDurationTimer();
            CurrentLead = null;
            _currentLeadMarkedAnswered = false;
            _currentLeadIndex = -1;
            _dialerStatus = DialerRunStatus.Stopped;
            StatusMessage = "Dialer stopped.";
            RefreshDashboard();
            RaiseDialerStateChanged();
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void MarkLeadPickedUp()
    {
        if (CurrentLead is null)
        {
            return;
        }

        _currentLeadMarkedAnswered = true;
        CurrentLead.QueueState = DialerLeadQueueState.Answered;
        StatusMessage = $"{CurrentLead.Name} marked as picked up. Press Hang Up when the Zoom conversation is finished.";
        RaiseDialerStateChanged();
    }

    private async Task HangUpAndContinueAsync()
    {
        if (CurrentLead is null || string.IsNullOrWhiteSpace(_currentExternalCallId))
        {
            return;
        }

        var finishedLead = CurrentLead;
        var wasPaused = _dialerStatus == DialerRunStatus.Paused;
        var wasAnswered = _currentLeadMarkedAnswered;
        var nextLead = default(DialerLeadRow);

        SetBusy(true);
        ClearMessages();
        StatusMessage = wasAnswered
            ? $"Ending answered call for {finishedLead.Name}..."
            : $"Hanging up {finishedLead.Name} as no answer...";

        try
        {
            await SafeHangUpAsync(
                wasAnswered: wasAnswered,
                requeueLead: false,
                outcomeLabel: wasAnswered ? "Answered" : "No answer");

            finishedLead.QueueState = wasAnswered ? DialerLeadQueueState.Answered : DialerLeadQueueState.NoAnswer;
            finishedLead.IsCurrent = false;
            StopCallDurationTimer();
            CurrentLead = null;
            _currentLeadMarkedAnswered = false;
            RefreshDashboard();

            if (wasPaused)
            {
                _dialerStatus = DialerRunStatus.Paused;
                StatusMessage = $"{finishedLead.Name} finished. Dialer is paused.";
            }
            else
            {
                nextLead = GetNextCallableLead(finishedLead.Id);
                _dialerStatus = nextLead is null ? DialerRunStatus.Completed : DialerRunStatus.Running;
                StatusMessage = nextLead is null
                    ? $"Call ended for {finishedLead.Name}. Queue completed."
                    : $"{finishedLead.Name} marked as {(wasAnswered ? "answered" : "no answer")}. Next call starts in 3 seconds.";
            }

            RaiseDialerStateChanged();
        }
        finally
        {
            SetBusy(false);
        }

        if (!wasPaused && nextLead is not null)
        {
            await WaitForNextLeadAndStartAsync(nextLead);
        }
    }

    private async Task SkipCurrentLeadAsync()
    {
        if (CurrentLead is null || string.IsNullOrWhiteSpace(_currentExternalCallId))
        {
            return;
        }

        var skippedLead = CurrentLead;
        SetBusy(true);
        ClearMessages();
        StatusMessage = $"Skipping {skippedLead.Name}...";

        try
        {
            await SafeHangUpAsync(
                wasAnswered: false,
                requeueLead: true,
                outcomeLabel: "Skipped");

            skippedLead.IsCurrent = false;
            RequeueLead(skippedLead);
            StopCallDurationTimer();
            CurrentLead = null;
            _currentLeadMarkedAnswered = false;
            _dialerStatus = DialerRunStatus.Running;
            RefreshDashboard();
            RaiseDialerStateChanged();
        }
        finally
        {
            SetBusy(false);
        }

        var nextLead = GetNextCallableLead(skippedLead.Id);
        if (nextLead is null)
        {
            _dialerStatus = DialerRunStatus.Completed;
            StatusMessage = "No other callable leads are available in the queue.";
            RaiseDialerStateChanged();
            return;
        }

        StatusMessage = $"{skippedLead.Name} moved to the end of the queue. Next call starts in 3 seconds.";
        RaiseDialerStateChanged();
        await WaitForNextLeadAndStartAsync(nextLead);
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
        CancelAutoAdvance();

        if (lead is null)
        {
            CurrentLead = null;
            StopCallDurationTimer();
            _currentLeadMarkedAnswered = false;
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
            StopCallDurationTimer();
            lead.QueueState = DialerLeadQueueState.NoAnswer;
            lead.IsCurrent = false;
            CurrentLead = null;
            _currentLeadMarkedAnswered = false;
            _dialerStatus = DialerRunStatus.Stopped;
            ErrorMessage = exception.Message;
            RaiseDialerStateChanged();
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task WaitForNextLeadAndStartAsync(DialerLeadRow nextLead)
    {
        CancelAutoAdvance();
        _autoAdvanceCancellationSource = new CancellationTokenSource();
        var cancellationToken = _autoAdvanceCancellationSource.Token;

        try
        {
            await Task.Delay(_zoomDesktopDialerClient.AutoNextDialDelayMs, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (cancellationToken.IsCancellationRequested || _dialerStatus != DialerRunStatus.Running)
        {
            return;
        }

        await StartLeadAsync(nextLead);
    }

    private void RequeueLead(DialerLeadRow lead)
    {
        lead.QueueState = DialerLeadQueueState.Pending;
        lead.StatusLabelOverride = null;

        var currentIndex = LeadQueue.IndexOf(lead);
        if (currentIndex >= 0)
        {
            LeadQueue.RemoveAt(currentIndex);
            LeadQueue.Add(lead);
        }

        ReindexQueueNumbers();
        _currentLeadIndex = LeadQueue.IndexOf(lead);
    }

    private void ReindexQueueNumbers()
    {
        for (var index = 0; index < LeadQueue.Count; index++)
        {
            LeadQueue[index].SetQueueNumber(index + 1);
        }
    }

    private async Task SafeHangUpAsync(bool wasAnswered, bool requeueLead, string outcomeLabel)
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
            await _apiClient.HangUpAsync(
                new HangUpCallRequest(
                    externalCallId,
                    WasAnswered: wasAnswered,
                    RequeueLead: requeueLead,
                    OutcomeLabel: outcomeLabel),
                CancellationToken.None);
        }
        catch (Exception exception)
        {
            ErrorMessage = exception.Message;
            StatusMessage = "Zoom desktop ended the call, but backend call logging needs attention.";
        }
    }
}
