namespace WinDeskReminder.Models;

public sealed class DailyReminderStat
{
    public string Date { get; set; } = string.Empty;
    public string ReminderId { get; set; } = string.Empty;
    public string ReminderName { get; set; } = string.Empty;
    public int Count { get; set; }
}
