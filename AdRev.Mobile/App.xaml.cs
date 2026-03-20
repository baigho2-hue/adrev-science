namespace AdRev.Mobile;

public partial class App : Application
{
	public App()
	{
		InitializeComponent();

        bool isSecurityEnabled = Preferences.Default.Get("IsSecurityEnabled", false);
        if (isSecurityEnabled)
        {
            MainPage = new SecurityPage();
        }
        else
        {
		    MainPage = new AppShell();
        }
	}
}
