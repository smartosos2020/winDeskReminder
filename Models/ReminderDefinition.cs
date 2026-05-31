namespace WinDeskReminder.Models;

public sealed class ReminderDefinition
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "提醒";
    public int WorkMinutes { get; set; } = 30;
    public int ActionMinutes { get; set; } = 5;
    public bool IsEnabled { get; set; } = true;

    public ReminderDefinition Clone()
    {
        return new ReminderDefinition
        {
            Id = Id,
            Name = Name,
            WorkMinutes = WorkMinutes,
            ActionMinutes = ActionMinutes,
            IsEnabled = IsEnabled
        };
    }
}
