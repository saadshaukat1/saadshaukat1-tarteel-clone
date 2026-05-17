using CommunityToolkit.Mvvm.ComponentModel;
using TarteelMobile.Models;
using TarteelMobile.Services;

namespace TarteelMobile.ViewModels;

public partial class ProgressViewModel : ObservableObject
{
    private readonly IApiService _api;

    [ObservableProperty] private List<Verse> _memorizedVerses = [];

    public ProgressViewModel(IApiService api) => _api = api;
}
