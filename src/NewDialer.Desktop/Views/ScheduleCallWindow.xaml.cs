using System.Globalization;
using System.Windows;
using NewDialer.Desktop.Models;

namespace NewDialer.Desktop.Views;

public partial class ScheduleCallWindow : Window
{
    public ScheduleCallWindow(string leadName)
    {
        InitializeComponent();
        LeadNameTextBlock.Text = $"Schedule callback for {leadName}";
        ScheduleDatePicker.SelectedDate = DateTime.Today.AddDays(1);
        TimeZoneComboBox.ItemsSource = TimeZoneInfo.GetSystemTimeZones();
        TimeZoneComboBox.SelectedItem = TimeZoneInfo.Local;
    }

    public ScheduledCallDraft? Draft { get; private set; }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (!ScheduleDatePicker.SelectedDate.HasValue)
        {
            MessageBox.Show("Select a schedule date.", "Schedule Call", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!TimeOnly.TryParseExact(TimeTextBox.Text.Trim(), "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var time))
        {
            MessageBox.Show("Enter time in HH:mm format, for example 14:30.", "Schedule Call", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var timeZone = TimeZoneComboBox.SelectedItem as TimeZoneInfo ?? TimeZoneInfo.Local;
        var scheduledDate = ScheduleDatePicker.SelectedDate.Value;
        var scheduledLocal = new DateTime(
            scheduledDate.Year,
            scheduledDate.Month,
            scheduledDate.Day,
            time.Hour,
            time.Minute,
            0);
        var unspecified = DateTime.SpecifyKind(scheduledLocal, DateTimeKind.Unspecified);
        var offset = timeZone.GetUtcOffset(unspecified);

        Draft = new ScheduledCallDraft(
            ScheduledForUtc: new DateTimeOffset(unspecified, offset).ToUniversalTime(),
            TimeZoneId: timeZone.Id,
            Notes: NotesTextBox.Text.Trim());

        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
