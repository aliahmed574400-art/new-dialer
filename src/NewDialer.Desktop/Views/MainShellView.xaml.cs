using System.Windows;
using System.Windows.Controls;
using NewDialer.Desktop.Models;
using NewDialer.Desktop.ViewModels;

namespace NewDialer.Desktop.Views;

public partial class MainShellView : UserControl
{
    public MainShellView()
    {
        InitializeComponent();
    }

    private async void ScheduleLead_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        if (viewModel.CurrentLead is null)
        {
            MessageBox.Show("Start the dialer or select an active lead before scheduling a callback.", "Schedule Call", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = new ScheduleCallWindow(viewModel.CurrentLead.Name)
        {
            Owner = Window.GetWindow(this),
        };

        if (dialog.ShowDialog() == true && dialog.Draft is not null)
        {
            await viewModel.CreateScheduleAsync(dialog.Draft);
        }
    }

    private async void ImportLeads_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            await viewModel.ImportLeadsFromPickerAsync();
        }
    }

    private async void AssignSelectedLeads_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        var selectedLeadIds = AdminLeadsGrid.SelectedItems
            .OfType<DialerLeadRow>()
            .Select(x => x.Id)
            .Where(x => x != Guid.Empty)
            .Distinct()
            .ToArray();

        if (selectedLeadIds.Length == 0)
        {
            MessageBox.Show("Select one or more leads before assigning them.", "Assign Leads", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        await viewModel.AssignLeadsAsync(selectedLeadIds);
    }
}
