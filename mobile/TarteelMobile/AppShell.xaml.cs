using TarteelMobile.Views;

namespace TarteelMobile;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();
        Routing.RegisterRoute("LoginPage", typeof(LoginPage));
    }
}
