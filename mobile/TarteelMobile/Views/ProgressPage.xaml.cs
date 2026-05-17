using TarteelMobile.ViewModels;

namespace TarteelMobile.Views;

public partial class ProgressPage : ContentPage
{
    public ProgressPage(ProgressViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
