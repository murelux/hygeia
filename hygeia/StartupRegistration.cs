using Microsoft.Win32;

namespace Hygeia;

public static class StartupRegistration
{
    const string ValueName = "hygeia";
    const string OldValueName = "Colorfire.Fan";
    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        return key?.GetValue(ValueName) is string text && text.Contains("--background", StringComparison.OrdinalIgnoreCase);
    }

    public static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        key.DeleteValue(OldValueName, false);
        if (!enabled)
        {
            key.DeleteValue(ValueName, false);
            return;
        }

        var exe = Environment.ProcessPath ?? "";
        key.SetValue(ValueName, $"\"{exe}\" --background");
    }
}
