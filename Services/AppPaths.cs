using System.IO;

namespace WinDeskReminder.Services;

public static class AppPaths
{
    public static string AppDataDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "WinDeskReminder");

    public static string ErrorLogPath { get; } = Path.Combine(AppDataDirectory, "error.log");

    public static string SettingsPath { get; } = Path.Combine(AppDataDirectory, "settings.json");

    public static string StatsPath { get; } = Path.Combine(AppDataDirectory, "stats.json");
}
