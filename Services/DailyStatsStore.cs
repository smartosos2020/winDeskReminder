using System.IO;
using System.Text.Json;
using WinDeskReminder.Models;

namespace WinDeskReminder.Services;

public sealed class DailyStatsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly List<DailyReminderStat> _stats;

    public DailyStatsStore()
    {
        _stats = Load();
    }

    public string StatsPath { get; } = AppPaths.StatsPath;

    public void RecordCompletion(ReminderItem item)
    {
        var today = DateTime.Today.ToString("yyyy-MM-dd");
        var stat = _stats.FirstOrDefault(entry => entry.Date == today && entry.ReminderId == item.Id);
        if (stat is null)
        {
            stat = new DailyReminderStat
            {
                Date = today,
                ReminderId = item.Id,
                ReminderName = item.Name
            };
            _stats.Add(stat);
        }

        stat.ReminderName = item.Name;
        stat.Count++;
        Save();
    }

    public IReadOnlyList<DailyReminderStat> GetTodayStats()
    {
        var today = DateTime.Today.ToString("yyyy-MM-dd");
        return _stats
            .Where(entry => entry.Date == today)
            .OrderBy(entry => entry.ReminderName)
            .ToList();
    }

    public int GetTodayTotal()
    {
        return GetTodayStats().Sum(entry => entry.Count);
    }

    private List<DailyReminderStat> Load()
    {
        try
        {
            if (!File.Exists(StatsPath))
            {
                return [];
            }

            var json = File.ReadAllText(StatsPath);
            return JsonSerializer.Deserialize<List<DailyReminderStat>>(json, JsonOptions) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private void Save()
    {
        var directory = Path.GetDirectoryName(StatsPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(StatsPath, JsonSerializer.Serialize(_stats, JsonOptions));
    }
}
