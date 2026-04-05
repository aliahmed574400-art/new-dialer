using System.Windows;
using System.ComponentModel;
using System.Windows.Threading;
using NewDialer.Desktop.Configuration;
using NewDialer.Desktop.Services;
using NewDialer.Desktop.ViewModels;
using NewDialer.Desktop.Views;

namespace NewDialer.Desktop;

public partial class MainWindow : Window
{
    private const double PreferredWidth = 1480;
    private const double PreferredHeight = 900;
    private const double MinimumUsableWidth = 820;
    private const double MinimumUsableHeight = 620;
    private const double WorkAreaPadding = 48;
    private const double EdgePadding = 12;

    private readonly MainShellView _mainShellView;
    private readonly MainWindowViewModel _viewModel;

    public MainWindow()
    {
        InitializeComponent();
        Title = "NewDialer";
        MinWidth = MinimumUsableWidth;
        MinHeight = MinimumUsableHeight;
        WindowStartupLocation = WindowStartupLocation.Manual;
        ConfigureInitialWindowBounds();
        Loaded += (_, _) => EnsureWindowVisible();

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
        var availableWidth = Math.Max(MinimumUsableWidth, workArea.Width - (EdgePadding * 2));
        var availableHeight = Math.Max(MinimumUsableHeight, workArea.Height - (EdgePadding * 2));

        Width = Math.Min(PreferredWidth, availableWidth);
        Height = Math.Min(PreferredHeight, availableHeight);
        MaxWidth = workArea.Width;
        MaxHeight = workArea.Height;

        Left = workArea.Left + ((workArea.Width - Width) / 2);
        Top = workArea.Top + ((workArea.Height - Height) / 2);
        EnsureWindowVisible();
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

        Dispatcher.BeginInvoke(EnsureWindowVisible, DispatcherPriority.Loaded);
    }

    private void EnsureWindowVisible()
    {
        if (WindowState != WindowState.Normal)
        {
            return;
        }

        var workArea = SystemParameters.WorkArea;
        var currentWidth = ActualWidth > 0 ? ActualWidth : Width;
        var currentHeight = ActualHeight > 0 ? ActualHeight : Height;

        if (currentWidth <= 0 || currentHeight <= 0)
        {
            return;
        }

        var targetWidth = Math.Min(currentWidth, Math.Max(MinimumUsableWidth, workArea.Width - (EdgePadding * 2)));
        var targetHeight = Math.Min(currentHeight, Math.Max(MinimumUsableHeight, workArea.Height - (EdgePadding * 2)));

        if (!double.IsNaN(targetWidth) && targetWidth > 0)
        {
            Width = targetWidth;
        }

        if (!double.IsNaN(targetHeight) && targetHeight > 0)
        {
            Height = targetHeight;
        }

        Left = Math.Max(workArea.Left + EdgePadding, workArea.Left + ((workArea.Width - Width) / 2));
        Top = Math.Max(workArea.Top + EdgePadding, workArea.Top + ((workArea.Height - Height) / 2));
    }
}
