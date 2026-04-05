using System.Windows.Controls;
using NewDialer.Desktop.ViewModels;

namespace NewDialer.Desktop.Views;

public partial class LoginView : UserControl
{
    public LoginView()
    {
        InitializeComponent();
    }

    private void LoginPasswordBox_OnPasswordChanged(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel && sender is PasswordBox passwordBox)
        {
            viewModel.SetLoginPassword(passwordBox.Password);
        }
    }

    private void SignupPasswordBox_OnPasswordChanged(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel && sender is PasswordBox passwordBox)
        {
            viewModel.SetSignupPassword(passwordBox.Password);
        }
    }

    private void SignupConfirmPasswordBox_OnPasswordChanged(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel && sender is PasswordBox passwordBox)
        {
            viewModel.SetSignupConfirmPassword(passwordBox.Password);
        }
    }
}
