using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace WinDeskReminder.Services;

public static class StartupService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "WinDeskReminder";

    public static void Apply(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true)
            ?? Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);

        if (enabled)
        {
            key.SetValue(AppName, $"\"{GetExecutablePath()}\"");
            return;
        }

        key.DeleteValue(AppName, throwOnMissingValue: false);
    }

    public static string GetExecutablePath()
    {
        return Environment.ProcessPath
            ?? Process.GetCurrentProcess().MainModule?.FileName
            ?? Path.Combine(AppContext.BaseDirectory, "WinDeskReminder.exe");
    }
}
