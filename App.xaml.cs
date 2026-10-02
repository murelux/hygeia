using System.Runtime.InteropServices;
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
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000))
        {
            MessageBox(IntPtr.Zero, "hygeia 只支持 64 位 Windows 11。\nhygeia requires 64-bit Windows 11.", "hygeia", 0x10);
            Environment.Exit(1);
            return;
        }

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

    [DllImport("user32", CharSet = CharSet.Unicode, EntryPoint = "MessageBoxW")]
    static extern int MessageBox(IntPtr owner, string text, string caption, uint type);
}