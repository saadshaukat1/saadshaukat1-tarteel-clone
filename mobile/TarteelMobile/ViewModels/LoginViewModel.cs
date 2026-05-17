using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TarteelMobile.Services;

namespace TarteelMobile.ViewModels;

public partial class LoginViewModel : ObservableObject
{
    private readonly IApiService _api;

    [ObservableProperty] private string _email    = string.Empty;
    [ObservableProperty] private string _password = string.Empty;
    [ObservableProperty] private string _error    = string.Empty;
    [ObservableProperty] private bool   _isBusy;

    public LoginViewModel(IApiService api) => _api = api;

    [RelayCommand]
    private async Task LoginAsync()
    {
        IsBusy = true;
        Error  = string.Empty;
        var ok = await _api.LoginAsync(Email, Password);
        if (!ok) Error = "Invalid email or password.";
        IsBusy = false;
    }

    [RelayCommand]
    private async Task RegisterAsync()
    {
        IsBusy = true;
        Error  = string.Empty;
        var ok = await _api.RegisterAsync(Email, Password);
        if (!ok) Error = "Registration failed. Please try again.";
        IsBusy = false;
    }
}
