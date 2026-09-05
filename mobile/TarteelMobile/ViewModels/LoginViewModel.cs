using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TarteelMobile.Services;

namespace TarteelMobile.ViewModels;

public partial class LoginViewModel : ObservableObject
{
    private readonly ISessionService _session;

    private string _email = string.Empty;
    public string Email
    {
        get => _email;
        set => SetProperty(ref _email, value);
    }

    private string _password = string.Empty;
    public string Password
    {
        get => _password;
        set => SetProperty(ref _password, value);
    }

    private string _error = string.Empty;
    public string Error
    {
        get => _error;
        set => SetProperty(ref _error, value);
    }

    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

    public LoginViewModel(ISessionService session) => _session = session;

    [RelayCommand]
    private async Task LoginAsync()
    {
        IsBusy = true;
        Error = string.Empty;
        var ok = await _session.LoginAsync(Email, Password);
        if (ok)
            await CompleteAuthenticationAsync();
        else
            Error = "Email or password is incorrect (min 4 chars).";
        IsBusy = false;
    }

    [RelayCommand]
    private async Task RegisterAsync()
    {
        IsBusy = true;
        Error = string.Empty;
        var ok = await _session.RegisterAsync(Email, Password);
        if (ok)
            await CompleteAuthenticationAsync();
        else
            Error = "Could not create account. Check your email format and password.";
        IsBusy = false;
    }

    [RelayCommand]
    private async Task ContinueOfflineAsync()
    {
        IsBusy = true;
        Error = string.Empty;
        var ok = await _session.LoginAsync("offline-default@tarteel.local", "offline123");
        if (!ok)
        {
            await _session.RegisterAsync("offline-default@tarteel.local", "offline123");
        }
        await CompleteAuthenticationAsync();
        IsBusy = false;
    }

    private async Task CompleteAuthenticationAsync()
    {
        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            if (Shell.Current is not null && Shell.Current.Navigation.ModalStack.Count > 0)
            {
                await Shell.Current.Navigation.PopModalAsync();
            }
            else
            {
                var shell = IPlatformApplication.Current!.Services.GetRequiredService<AppShell>();
                if (Application.Current?.Windows.Count > 0)
                {
                    Application.Current.Windows[0].Page = shell;
                }
            }
        });
    }
}
