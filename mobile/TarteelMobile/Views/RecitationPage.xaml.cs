using TarteelMobile.ViewModels;

namespace TarteelMobile.Views;

public partial class RecitationPage : ContentPage
{
    public RecitationPage(RecitationViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
