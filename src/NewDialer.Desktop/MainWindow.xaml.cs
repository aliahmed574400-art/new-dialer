using System.Windows;
using System.ComponentModel;
using NewDialer.Desktop.Configuration;
using NewDialer.Desktop.Services;
using NewDialer.Desktop.ViewModels;
using NewDialer.Desktop.Views;

namespace NewDialer.Desktop;

public partial class MainWindow : Window
{
    private const double PreferredWidth = 1480;
    private const double PreferredHeight = 900;
    private const double MinimumUsableWidth = 1024;
    private const double MinimumUsableHeight = 720;
    private const double WorkAreaPadding = 48;

    private readonly MainShellView _mainShellView;
    private readonly MainWindowViewModel _viewModel;

    public MainWindow()
    {
        InitializeComponent();
        Title = "NewDialer";
        MinWidth = 960;
        MinHeight = 680;
        WindowStartupLocation = WindowStartupLocation.Manual;
        ConfigureInitialWindowBounds();

        var options = DesktopAppOptions.Load();
        _viewModel = new MainWindowViewModel(
            new NewDialerApiClient(options.ApiBaseUrl),
            new ZoomDesktopDialerClient(options));
        _viewModel.PropertyChanged += ViewModelOnPropertyChanged;

        DataContext = _viewModel;

        _mainShellView = new MainShellView
        {
            DataContext = _viewModel,
        };

        UpdateContent();
    }

    private void ConfigureInitialWindowBounds()
    {
        var workArea = SystemParameters.WorkArea;
        var availableWidth = Math.Max(MinimumUsableWidth, workArea.Width - WorkAreaPadding);
        var availableHeight = Math.Max(MinimumUsableHeight, workArea.Height - WorkAreaPadding);

        Width = Math.Min(PreferredWidth, availableWidth);
        Height = Math.Min(PreferredHeight, availableHeight);
        MaxWidth = workArea.Width;
        MaxHeight = workArea.Height;

        Left = workArea.Left + ((workArea.Width - Width) / 2);
        Top = workArea.Top + ((workArea.Height - Height) / 2);
    }

    private void ViewModelOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.IsAuthenticated))
        {
            UpdateContent();
        }
    }

    private void UpdateContent()
    {
        Content = _viewModel.IsAuthenticated
            ? _mainShellView
            : new LoginView { DataContext = _viewModel };
    }
}
