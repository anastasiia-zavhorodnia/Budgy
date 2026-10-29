using Budgy.Services;

namespace Budgy;

public partial class App : Application
{
    public App(ISettingsService settings)
    {
        InitializeComponent();
        settings.ApplyTheme();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = new Window(new AppShell());

#if WINDOWS
        window.Title = "🦜💰 Budgy";

        window.TitleBar = new TitleBar
        {
            Title = "🦜💰 Budgy",
            BackgroundColor = Color.FromArgb("#1E1B3A"),
            ForegroundColor = Colors.White
        };
#endif

        return window;
    }

}
