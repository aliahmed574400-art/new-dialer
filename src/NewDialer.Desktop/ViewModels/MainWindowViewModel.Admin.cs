using Microsoft.Win32;
using NewDialer.Contracts.Analytics;
using NewDialer.Contracts.Leads;
using NewDialer.Desktop.Models;

namespace NewDialer.Desktop.ViewModels;

public sealed partial class MainWindowViewModel
{
    private AgentAssignmentOptionDto? _selectedImportAgent;
    private AgentAssignmentOptionDto? _selectedAssignmentAgent;
    private string _importNotes = string.Empty;

    public AgentAssignmentOptionDto? SelectedImportAgent
    {
        get => _selectedImportAgent;
        set
        {
            if (SetProperty(ref _selectedImportAgent, value))
            {
                OnPropertyChanged(nameof(CanManageLeads));
            }
        }
    }

    public AgentAssignmentOptionDto? SelectedAssignmentAgent
    {
        get => _selectedAssignmentAgent;
        set
        {
            if (SetProperty(ref _selectedAssignmentAgent, value))
            {
                OnPropertyChanged(nameof(CanManageLeads));
            }
        }
    }

    public string ImportNotes
    {
        get => _importNotes;
        set => SetProperty(ref _importNotes, value);
    }

    public bool CanManageLeads => IsAdmin && CanViewData && !IsBusy;

    public async Task ImportLeadsFromPickerAsync()
    {
        if (!CanManageLeads)
        {
            return;
        }

        var dialog = new OpenFileDialog
        {
            Filter = "Excel Workbook (*.xlsx)|*.xlsx",
            Multiselect = false,
            CheckFileExists = true,
            Title = "Select Leads Excel File",
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        SetBusy(true);
        ClearMessages();
        StatusMessage = "Uploading Excel leads...";

        try
        {
            var result = await _apiClient.ImportLeadsAsync(
                dialog.FileName,
                SelectedImportAgent?.AgentId,
                ImportNotes,
                CancellationToken.None);

            await LoadWorkspaceDataAsync();
            StatusMessage = result.Message;
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

    public async Task AssignLeadsAsync(IReadOnlyCollection<Guid> leadIds)
    {
        if (!CanManageLeads)
        {
            return;
        }

        if (SelectedAssignmentAgent is null)
        {
            ErrorMessage = "Select an agent before assigning leads.";
            return;
        }

        if (leadIds.Count == 0)
        {
            return;
        }

        SetBusy(true);
        ClearMessages();
        StatusMessage = $"Assigning {leadIds.Count} lead(s) to {SelectedAssignmentAgent.FullName}...";

        try
        {
            await _apiClient.AssignLeadsAsync(SelectedAssignmentAgent.AgentId, leadIds, CancellationToken.None);
            await LoadWorkspaceDataAsync();
            StatusMessage = $"Assigned {leadIds.Count} lead(s) to {SelectedAssignmentAgent.FullName}.";
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

    private void PopulateAgentPerformance(IEnumerable<AgentPerformanceDto> rows)
    {
        AgentPerformance.Clear();
        foreach (var row in rows)
        {
            AgentPerformance.Add(new AgentPerformanceRow
            {
                AgentName = row.AgentName,
                CallsToday = row.CallsToday,
                TalkTime = TimeSpan.FromSeconds(row.TalkSeconds).ToString(@"hh\:mm\:ss"),
                CheckIn = row.CheckInAtUtc?.ToLocalTime().ToString("HH:mm") ?? "-",
                CheckOut = row.CheckOutAtUtc?.ToLocalTime().ToString("HH:mm") ?? "-",
            });
        }
    }

    private void PopulateAgentOptions(IEnumerable<AgentAssignmentOptionDto> options)
    {
        AgentOptions.Clear();
        foreach (var option in options)
        {
            AgentOptions.Add(option);
        }

        if (SelectedImportAgent is not null)
        {
            SelectedImportAgent = AgentOptions.FirstOrDefault(x => x.AgentId == SelectedImportAgent.AgentId);
        }

        if (SelectedAssignmentAgent is not null)
        {
            SelectedAssignmentAgent = AgentOptions.FirstOrDefault(x => x.AgentId == SelectedAssignmentAgent.AgentId);
        }
    }

    private void PopulateImportBatches(IEnumerable<LeadImportBatchDto> batches)
    {
        ImportBatches.Clear();
        foreach (var batch in batches)
        {
            ImportBatches.Add(batch);
        }
    }
}
