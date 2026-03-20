namespace AdRev.Mobile;

public partial class SecurityPage : ContentPage
{
    public SecurityPage()
    {
        InitializeComponent();
    }

    private async void OnUnlockClicked(object sender, EventArgs e)
    {
        string savedPin = Preferences.Default.Get("SecurityPin", "0000");
        if (PinEntry.Text == savedPin)
        {
            App.Current.MainPage = new AppShell();
        }
        else
        {
            ErrorLabel.IsVisible = true;
            PinEntry.Text = string.Empty;
        }
    }
}
