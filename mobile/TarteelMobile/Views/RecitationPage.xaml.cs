using TarteelMobile.ViewModels;

namespace TarteelMobile.Views;

public partial class RecitationPage : ContentPage
{
    private readonly RecitationViewModel _vm;

    public RecitationPage(RecitationViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
        _vm = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _vm.RefreshAuthState();
    }
}
