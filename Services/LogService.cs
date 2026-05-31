using System.Diagnostics;
using System.IO;

namespace WinDeskReminder.Services;

public static class LogService
{
    public static void Write(Exception exception)
    {
        try
        {
            Directory.CreateDirectory(AppPaths.AppDataDirectory);
            File.AppendAllText(
                AppPaths.ErrorLogPath,
                $"[{DateTimeOffset.Now:O}]{Environment.NewLine}{exception}{Environment.NewLine}{Environment.NewLine}");
        }
        catch
        {
            // Logging must not create a second failure.
        }
    }

    public static void OpenLogDirectory()
    {
        Directory.CreateDirectory(AppPaths.AppDataDirectory);
        Process.Start(new ProcessStartInfo
        {
            FileName = AppPaths.AppDataDirectory,
            UseShellExecute = true
        });
    }
}
