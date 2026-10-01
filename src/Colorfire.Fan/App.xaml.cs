using Microsoft.UI.Xaml;

namespace Hygeia;

public partial class App : Application
{
    private Window? _window;

    public App()
    {
        InitializeComponent();
        UnhandledException += (_, e) =>
        {
            FanSafety.Release();
            e.Handled = false;
        };
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        var commandLine = Environment.GetCommandLineArgs();
        if (commandLine.Any(arg => arg == "--verify"))
        {
            Environment.Exit(FanCheck.Run());
            return;
        }

        if (commandLine.Any(arg => arg == "--status"))
        {
            Environment.Exit(FanCheck.Status());
            return;
        }

        var background = commandLine.Any(arg => arg == "--background");
        _window = new MainWindow(background);
        _window.Activate();
    }
}