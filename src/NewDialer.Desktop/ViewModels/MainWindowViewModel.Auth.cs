using NewDialer.Contracts.Auth;

namespace NewDialer.Desktop.ViewModels;

public sealed partial class MainWindowViewModel
{
    private string _loginIdentity = string.Empty;
    private string _loginWorkspaceKey = string.Empty;
    private string _loginPassword = string.Empty;
    private bool _isSignupMode;
    private string _signupFullName = string.Empty;
    private string _signupEmail = string.Empty;
    private string _signupCompanyName = string.Empty;
    private string _signupPhoneNumber = string.Empty;
    private string _signupPassword = string.Empty;
    private string _signupConfirmPassword = string.Empty;

    public string ApiBaseUrl
    {
        get => _apiBaseUrl;
        set
        {
            if (SetProperty(ref _apiBaseUrl, value))
            {
                RaiseAuthenticationStateChanged();
            }
        }
    }

    public string LoginIdentity
    {
        get => _loginIdentity;
        set
        {
            if (SetProperty(ref _loginIdentity, value))
            {
                RaiseAuthenticationStateChanged();
            }
        }
    }

    public string LoginWorkspaceKey
    {
        get => _loginWorkspaceKey;
        set
        {
            if (SetProperty(ref _loginWorkspaceKey, value))
            {
                RaiseAuthenticationStateChanged();
            }
        }
    }

    public string SignupFullName
    {
        get => _signupFullName;
        set
        {
            if (SetProperty(ref _signupFullName, value))
            {
                RaiseAuthenticationStateChanged();
            }
        }
    }

    public string SignupEmail
    {
        get => _signupEmail;
        set
        {
            if (SetProperty(ref _signupEmail, value))
            {
                RaiseAuthenticationStateChanged();
            }
        }
    }

    public string SignupCompanyName
    {
        get => _signupCompanyName;
        set
        {
            if (SetProperty(ref _signupCompanyName, value))
            {
                RaiseAuthenticationStateChanged();
            }
        }
    }

    public string SignupPhoneNumber
    {
        get => _signupPhoneNumber;
        set
        {
            if (SetProperty(ref _signupPhoneNumber, value))
            {
                RaiseAuthenticationStateChanged();
            }
        }
    }

    public bool IsSignupMode
    {
        get => _isSignupMode;
        set
        {
            if (SetProperty(ref _isSignupMode, value))
            {
                ClearMessages();
                OnPropertyChanged(nameof(IsLoginMode));
                OnPropertyChanged(nameof(AuthenticationTitle));
                OnPropertyChanged(nameof(AuthenticationSubtitle));
                OnPropertyChanged(nameof(AuthenticationSubmitText));
                OnPropertyChanged(nameof(AuthenticationSwitchText));
                RaiseAuthenticationStateChanged();
            }
        }
    }

    public bool IsLoginMode => !IsSignupMode;

    public string AuthenticationTitle => IsSignupMode ? "Create Admin Workspace" : "Sign In";

    public string AuthenticationSubtitle => IsSignupMode
        ? "Launch a new tenant with the 15-day trial, then start adding agents and importing leads."
        : "Admins and agents can sign in with email. Agent usernames with workspace keys are also supported.";

    public string AuthenticationSubmitText => IsSignupMode ? "Create Workspace" : "Sign In";

    public string AuthenticationSwitchText => IsSignupMode
        ? "Already have a workspace? Switch to sign in."
        : "Need a new tenant? Create an admin workspace.";

    public bool CanAuthenticate => !IsBusy && (IsSignupMode ? CanSubmitSignup() : CanSubmitLogin());

    public void SetLoginPassword(string password)
    {
        _loginPassword = password;
        RaiseAuthenticationStateChanged();
    }

    public void SetSignupPassword(string password)
    {
        _signupPassword = password;
        RaiseAuthenticationStateChanged();
    }

    public void SetSignupConfirmPassword(string password)
    {
        _signupConfirmPassword = password;
        RaiseAuthenticationStateChanged();
    }

    private async Task AuthenticateAsync()
    {
        SetBusy(true);
        ClearMessages();
        _apiClient.ApiBaseUrl = ApiBaseUrl;
        StatusMessage = IsSignupMode ? "Creating workspace..." : "Signing in...";

        try
        {
            SessionDto session;
            if (IsSignupMode)
            {
                if (!string.Equals(_signupPassword, _signupConfirmPassword, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Confirm password must match the password.");
                }

                session = await _apiClient.SignupAdminAsync(
                    new AdminSignupRequest(
                        SignupFullName.Trim(),
                        SignupEmail.Trim(),
                        SignupCompanyName.Trim(),
                        _signupPassword,
                        SignupPhoneNumber.Trim()),
                    CancellationToken.None);
            }
            else
            {
                session = await _apiClient.LoginAsync(
                    new LoginRequest(
                        LoginIdentity.Trim(),
                        _loginPassword,
                        string.IsNullOrWhiteSpace(LoginWorkspaceKey) ? null : LoginWorkspaceKey.Trim()),
                    CancellationToken.None);
            }

            ApplySession(session);
            await LoadWorkspaceDataAsync();
            StatusMessage = IsSignupMode
                ? $"Workspace created. Your workspace key is {session.WorkspaceKey}."
                : $"Signed in to {session.CompanyName}.";
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

    private async Task SignOutAsync()
    {
        SetBusy(true);
        ClearMessages();
        StatusMessage = "Signing out...";

        try
        {
            if (!string.IsNullOrWhiteSpace(_currentExternalCallId))
            {
                await SafeHangUpAsync();
            }

            if (IsAuthenticated)
            {
                await _apiClient.LogoutAsync(CancellationToken.None);
            }

            ResetSession();
            StatusMessage = "Signed out.";
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void ToggleAuthenticationMode()
    {
        IsSignupMode = !IsSignupMode;
    }

    private bool CanSubmitLogin()
    {
        return !string.IsNullOrWhiteSpace(ApiBaseUrl)
            && !string.IsNullOrWhiteSpace(LoginIdentity)
            && !string.IsNullOrWhiteSpace(_loginPassword);
    }

    private bool CanSubmitSignup()
    {
        return !string.IsNullOrWhiteSpace(ApiBaseUrl)
            && !string.IsNullOrWhiteSpace(SignupFullName)
            && !string.IsNullOrWhiteSpace(SignupEmail)
            && !string.IsNullOrWhiteSpace(SignupCompanyName)
            && !string.IsNullOrWhiteSpace(SignupPhoneNumber)
            && !string.IsNullOrWhiteSpace(_signupPassword)
            && !string.IsNullOrWhiteSpace(_signupConfirmPassword);
    }

    private void RaiseAuthenticationStateChanged()
    {
        OnPropertyChanged(nameof(CanAuthenticate));
        _authenticateCommand.RaiseCanExecuteChanged();
    }

    private void ResetAuthenticationForms()
    {
        LoginIdentity = string.Empty;
        LoginWorkspaceKey = string.Empty;
        _loginPassword = string.Empty;
        SignupFullName = string.Empty;
        SignupEmail = string.Empty;
        SignupCompanyName = string.Empty;
        SignupPhoneNumber = string.Empty;
        _signupPassword = string.Empty;
        _signupConfirmPassword = string.Empty;
        IsSignupMode = false;
    }
}
