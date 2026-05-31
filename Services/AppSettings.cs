using WinDeskReminder.Models;

namespace WinDeskReminder.Services;

public sealed class AppSettings
{
    public bool WidgetEnabled { get; set; } = true;
    public bool SoundEnabled { get; set; } = true;
    public bool StartWithWindows { get; set; }
    public bool QuietHoursEnabled { get; set; }
    public string QuietHoursStart { get; set; } = "22:00";
    public string QuietHoursEnd { get; set; } = "08:00";
    public DockEdge DockEdge { get; set; } = DockEdge.Right;
    public string? ScreenDeviceName { get; set; }
    public bool IsDocked { get; set; } = true;
    public double DockOffsetRatio { get; set; } = 0.5;
    public double BackdropOpacity { get; set; } = 0.84;
    public int IdlePauseMinutes { get; set; } = 5;
    public List<ReminderDefinition> Reminders { get; set; } = CreateDefaultReminders();

    public AppSettings Clone()
    {
        return new AppSettings
        {
            WidgetEnabled = WidgetEnabled,
            SoundEnabled = SoundEnabled,
            StartWithWindows = StartWithWindows,
            QuietHoursEnabled = QuietHoursEnabled,
            QuietHoursStart = QuietHoursStart,
            QuietHoursEnd = QuietHoursEnd,
            DockEdge = DockEdge,
            ScreenDeviceName = ScreenDeviceName,
            IsDocked = IsDocked,
            DockOffsetRatio = DockOffsetRatio,
            BackdropOpacity = BackdropOpacity,
            IdlePauseMinutes = IdlePauseMinutes,
            Reminders = Reminders.Select(item => item.Clone()).ToList()
        };
    }

    public void CopyFrom(AppSettings source)
    {
        WidgetEnabled = source.WidgetEnabled;
        SoundEnabled = source.SoundEnabled;
        StartWithWindows = source.StartWithWindows;
        QuietHoursEnabled = source.QuietHoursEnabled;
        QuietHoursStart = NormalizeTimeText(source.QuietHoursStart, "22:00");
        QuietHoursEnd = NormalizeTimeText(source.QuietHoursEnd, "08:00");
        DockEdge = source.DockEdge;
        ScreenDeviceName = source.ScreenDeviceName;
        IsDocked = source.IsDocked;
        DockOffsetRatio = Math.Clamp(source.DockOffsetRatio, 0, 1);
        BackdropOpacity = Math.Clamp(source.BackdropOpacity, 0.25, 1);
        IdlePauseMinutes = Math.Max(1, source.IdlePauseMinutes);
        Reminders = source.Reminders.Select(item => item.Clone()).ToList();
    }

    public static List<ReminderDefinition> CreateDefaultReminders()
    {
        return
        [
            new ReminderDefinition
            {
                Id = "stand",
                Name = "站立活动",
                IconKind = "stand",
                WorkMinutes = 45,
                ActionMinutes = 5,
                IsEnabled = true,
                SoundEnabled = true
            },
            new ReminderDefinition
            {
                Id = "water",
                Name = "喝水",
                IconKind = "water",
                WorkMinutes = 30,
                ActionMinutes = 1,
                IsEnabled = true,
                SoundEnabled = true
            },
            new ReminderDefinition
            {
                Id = "rest",
                Name = "休息眼睛",
                IconKind = "eye",
                WorkMinutes = 60,
                ActionMinutes = 5,
                IsEnabled = true,
                SoundEnabled = true
            }
        ];
    }

    public static string NormalizeTimeText(string? value, string fallback)
    {
        return TimeOnly.TryParse(value, out var time)
            ? time.ToString("HH:mm")
            : fallback;
    }
}
