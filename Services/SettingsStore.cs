using System.IO;
using System.Text.Json;

namespace WinDeskReminder.Services;

public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public string SettingsPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "WinDeskReminder",
        "settings.json");

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsPath))
            {
                return new AppSettings();
            }

            var json = File.ReadAllText(SettingsPath);
            var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
            if (!json.Contains("\"SoundEnabled\"", StringComparison.Ordinal))
            {
                settings.SoundEnabled = true;
            }

            if (settings.Reminders.Count == 0)
            {
                settings.Reminders = AppSettings.CreateDefaultReminders();
            }

            settings.BackdropOpacity = Math.Clamp(settings.BackdropOpacity, 0.25, 1);
            settings.DockOffsetRatio = Math.Clamp(settings.DockOffsetRatio, 0, 1);
            settings.IdlePauseMinutes = Math.Max(1, settings.IdlePauseMinutes);
            foreach (var reminder in settings.Reminders)
            {
                if (string.IsNullOrWhiteSpace(reminder.Id))
                {
                    reminder.Id = Guid.NewGuid().ToString("N");
                }
            }
            return settings;
        }
        catch
        {
            BackupInvalidSettings();
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        var directory = Path.GetDirectoryName(SettingsPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(settings, JsonOptions);
        var temporaryPath = SettingsPath + ".tmp";
        var backupPath = SettingsPath + ".bak";

        File.WriteAllText(temporaryPath, json);
        if (File.Exists(SettingsPath))
        {
            File.Replace(temporaryPath, SettingsPath, backupPath, ignoreMetadataErrors: true);
        }
        else
        {
            File.Move(temporaryPath, SettingsPath);
        }
    }

    private void BackupInvalidSettings()
    {
        try
        {
            if (!File.Exists(SettingsPath))
            {
                return;
            }

            var invalidPath = Path.Combine(
                Path.GetDirectoryName(SettingsPath)!,
                $"settings.invalid-{DateTimeOffset.Now:yyyyMMddHHmmss}.json");
            File.Move(SettingsPath, invalidPath, overwrite: true);
        }
        catch
        {
            // A broken settings file should not prevent the app from starting.
        }
    }
}
