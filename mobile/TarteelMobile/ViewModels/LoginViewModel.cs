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
        Error  = string.Empty;
        var ok = await _session.LoginAsync(Email, Password);
        if (ok)
            await Shell.Current.Navigation.PopModalAsync();
        else
            Error = "Email or password is incorrect.";
        IsBusy = false;
    }

    [RelayCommand]
    private async Task RegisterAsync()
    {
        IsBusy = true;
        Error  = string.Empty;
        var ok = await _session.RegisterAsync(Email, Password);
        if (ok)
            await Shell.Current.Navigation.PopModalAsync();
        else
            Error = "Could not create account. Check your email address and try again.";
        IsBusy = false;
    }
}
