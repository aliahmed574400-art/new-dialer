using NewDialer.Contracts.Agents;
using NewDialer.Desktop.Models;

namespace NewDialer.Desktop.ViewModels;

public sealed partial class MainWindowViewModel
{
    private string _agentFormFullName = string.Empty;
    private string _agentFormEmail = string.Empty;
    private string _agentFormPassword = string.Empty;

    public string AgentFormFullName
    {
        get => _agentFormFullName;
        set
        {
            if (SetProperty(ref _agentFormFullName, value))
            {
                RaiseAgentEditorStateChanged();
            }
        }
    }

    public string AgentFormEmail
    {
        get => _agentFormEmail;
        set
        {
            if (SetProperty(ref _agentFormEmail, value))
            {
                RaiseAgentEditorStateChanged();
            }
        }
    }

    public string AgentFormTitle => SelectedAgent is null ? "Create Agent" : "Edit Agent";

    public string AgentFormDescription => SelectedAgent is null
        ? "Set the agent's name, email, and password. They can sign in with the same email and password."
        : "Update the selected agent's profile. Leave password blank to keep the current password.";

    public string AgentPrimaryButtonText => SelectedAgent is null ? "Create Agent" : "Save Agent Changes";

    public bool CanManageAgents => IsAdmin && CanViewData && !IsBusy;

    public bool CanSaveAgent => CanManageAgents
        && !string.IsNullOrWhiteSpace(AgentFormFullName)
        && !string.IsNullOrWhiteSpace(AgentFormEmail)
        && (SelectedAgent is not null || !string.IsNullOrWhiteSpace(_agentFormPassword));

    public bool CanDeleteSelectedAgent => CanManageAgents && SelectedAgent is not null;

    public bool HasSelectedAgent => SelectedAgent is not null;

    public string SelectedAgentNameDisplay => SelectedAgent?.FullName ?? "Select an agent";

    public string SelectedAgentEmailDisplay => SelectedAgent?.Email ?? "Agent profile details appear here";

    public string SelectedAgentUsernameDisplay => SelectedAgent?.Username ?? "Username is generated automatically";

    public string SelectedAgentCallsTodayDisplay => SelectedAgent?.CallsToday.ToString() ?? "0";

    public string SelectedAgentScheduledCallsDisplay => SelectedAgent?.ScheduledCalls.ToString() ?? "0";

    public string SelectedAgentTalkTimeDisplay => SelectedAgent?.TalkTime ?? "00:00:00";

    public string SelectedAgentCheckInDisplay => SelectedAgent?.CheckIn ?? "-";

    public string SelectedAgentCheckOutDisplay => SelectedAgent?.CheckOut ?? "-";

    public void SetAgentPassword(string password)
    {
        _agentFormPassword = password;
        RaiseAgentEditorStateChanged();
    }

    public async Task<bool> SaveAgentAsync()
    {
        if (!CanManageAgents)
        {
            return false;
        }

        var isCreate = SelectedAgent is null;
        SetBusy(true);
        ClearMessages();
        StatusMessage = isCreate
            ? "Creating agent..."
            : $"Saving {SelectedAgent!.FullName}...";

        try
        {
            AgentAdminDto result;
            if (isCreate)
            {
                result = await _apiClient.CreateAgentAsync(
                    new CreateAgentRequest(
                        AgentFormFullName.Trim(),
                        AgentFormEmail.Trim(),
                        _agentFormPassword),
                    CancellationToken.None);
            }
            else
            {
                result = await _apiClient.UpdateAgentAsync(
                    SelectedAgent!.AgentId,
                    new UpdateAgentRequest(
                        AgentFormFullName.Trim(),
                        AgentFormEmail.Trim(),
                        string.IsNullOrWhiteSpace(_agentFormPassword) ? null : _agentFormPassword),
                    CancellationToken.None);
            }

            _agentFormPassword = string.Empty;
            await LoadWorkspaceDataAsync(result.AgentId);
            StatusMessage = isCreate
                ? $"{result.FullName} is ready to sign in as an agent."
                : $"{result.FullName} was updated.";
            RaiseAgentEditorStateChanged();
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

    public async Task<bool> DeleteSelectedAgentAsync()
    {
        if (!CanDeleteSelectedAgent || SelectedAgent is null)
        {
            return false;
        }

        var agentId = SelectedAgent.AgentId;
        var agentName = SelectedAgent.FullName;

        SetBusy(true);
        ClearMessages();
        StatusMessage = $"Deleting {agentName}...";

        try
        {
            await _apiClient.DeleteAgentAsync(agentId, CancellationToken.None);
            ClearAgentForm();
            await LoadWorkspaceDataAsync();
            StatusMessage = $"{agentName} was removed from active agent access.";
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

    public void ClearAgentForm()
    {
        if (SelectedAgent is not null)
        {
            SelectedAgent = null;
            return;
        }

        ApplyAgentSelection(null);
    }

    private void ApplyAgentSelection(AgentAdminRow? selectedAgent)
    {
        if (selectedAgent is null)
        {
            _agentFormFullName = string.Empty;
            _agentFormEmail = string.Empty;
        }
        else
        {
            _agentFormFullName = selectedAgent.FullName;
            _agentFormEmail = selectedAgent.Email;
        }

        _agentFormPassword = string.Empty;

        OnPropertyChanged(nameof(AgentFormFullName));
        OnPropertyChanged(nameof(AgentFormEmail));
        OnPropertyChanged(nameof(AgentFormTitle));
        OnPropertyChanged(nameof(AgentFormDescription));
        OnPropertyChanged(nameof(AgentPrimaryButtonText));
        OnPropertyChanged(nameof(HasSelectedAgent));
        OnPropertyChanged(nameof(SelectedAgentNameDisplay));
        OnPropertyChanged(nameof(SelectedAgentEmailDisplay));
        OnPropertyChanged(nameof(SelectedAgentUsernameDisplay));
        OnPropertyChanged(nameof(SelectedAgentCallsTodayDisplay));
        OnPropertyChanged(nameof(SelectedAgentScheduledCallsDisplay));
        OnPropertyChanged(nameof(SelectedAgentTalkTimeDisplay));
        OnPropertyChanged(nameof(SelectedAgentCheckInDisplay));
        OnPropertyChanged(nameof(SelectedAgentCheckOutDisplay));
        RaiseAgentEditorStateChanged();
    }

    private void PopulateAgents(IEnumerable<AgentAdminDto> agents, Guid? preferredSelectedAgentId)
    {
        Agents.Clear();
        foreach (var agent in agents)
        {
            Agents.Add(new AgentAdminRow
            {
                AgentId = agent.AgentId,
                FullName = agent.FullName,
                Email = agent.Email,
                Username = agent.Username,
                CallsToday = agent.CallsToday,
                ScheduledCalls = agent.ScheduledCalls,
                TalkTime = TimeSpan.FromSeconds(agent.TalkSeconds).ToString(@"hh\:mm\:ss"),
                CheckIn = agent.CheckInAtUtc?.ToLocalTime().ToString("HH:mm") ?? "-",
                CheckOut = agent.CheckOutAtUtc?.ToLocalTime().ToString("HH:mm") ?? "-",
            });
        }

        if (preferredSelectedAgentId.HasValue)
        {
            SelectedAgent = Agents.FirstOrDefault(x => x.AgentId == preferredSelectedAgentId.Value);
            return;
        }

        if (SelectedAgent is not null)
        {
            SelectedAgent = Agents.FirstOrDefault(x => x.AgentId == SelectedAgent.AgentId);
            return;
        }

        ApplyAgentSelection(null);
    }

    private void RaiseAgentEditorStateChanged()
    {
        OnPropertyChanged(nameof(CanManageAgents));
        OnPropertyChanged(nameof(CanSaveAgent));
        OnPropertyChanged(nameof(CanDeleteSelectedAgent));
    }
}
