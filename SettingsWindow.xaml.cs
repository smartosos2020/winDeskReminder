using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using WinDeskReminder.Models;
using WinDeskReminder.Services;

namespace WinDeskReminder;

public partial class SettingsWindow : Window
{
    private readonly AppSettings _settings;
    private readonly SettingsStore _settingsStore;
    private readonly ReminderController _controller;
    private readonly MainWindow _widget;
    private readonly AppSettings _draft;

    public SettingsWindow(
        AppSettings settings,
        SettingsStore settingsStore,
        ReminderController controller,
        MainWindow widget)
    {
        _settings = settings;
        _settingsStore = settingsStore;
        _controller = controller;
        _widget = widget;
        _draft = settings.Clone();

        InitializeComponent();
        DataContext = _draft;

        EdgeBox.ItemsSource = Enum.GetValues<DockEdge>();
        ScreenBox.ItemsSource = BuildScreenOptions();
        if (_draft.ScreenDeviceName is null)
        {
            ScreenBox.SelectedIndex = 0;
        }
    }

    private static List<ScreenOption> BuildScreenOptions()
    {
        var options = new List<ScreenOption>
        {
            new("主显示器", null)
        };

        options.AddRange(Screen.AllScreens.Select((screen, index) =>
            new ScreenOption($"显示器 {index + 1} {(screen.Primary ? "(主)" : string.Empty)} {screen.Bounds.Width}x{screen.Bounds.Height}",
                screen.DeviceName)));
        return options;
    }

    private void AddReminder_Click(object sender, RoutedEventArgs e)
    {
        _draft.Reminders.Add(new ReminderDefinition
        {
            Name = "新提醒",
            IconKind = "stand",
            WorkMinutes = 30,
            ActionMinutes = 5,
            IsEnabled = true,
            SoundEnabled = true
        });

        ReminderList.Items.Refresh();
    }

    private void DeleteReminder_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is ReminderDefinition selected)
        {
            _draft.Reminders.Remove(selected);
        }

        if (_draft.Reminders.Count == 0)
        {
            _draft.Reminders.Add(new ReminderDefinition
            {
                Name = "新提醒",
                IconKind = "stand",
                WorkMinutes = 30,
                ActionMinutes = 5,
                IsEnabled = true,
                SoundEnabled = true
            });
        }

        ReminderList.Items.Refresh();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        NormalizeDraft();
        var remindersChanged = HaveRemindersChanged(_settings.Reminders, _draft.Reminders);
        _settings.CopyFrom(_draft);
        _settingsStore.Save(_settings);
        StartupService.Apply(_settings.StartWithWindows);
        if (remindersChanged)
        {
            _controller.ApplySettings(_settings);
        }

        _widget.ApplySettings();
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void NormalizeDraft()
    {
        _draft.BackdropOpacity = Math.Clamp(_draft.BackdropOpacity, 0.25, 1);
        _draft.IdlePauseMinutes = Math.Max(1, _draft.IdlePauseMinutes);
        _draft.QuietHoursStart = AppSettings.NormalizeTimeText(_draft.QuietHoursStart, "22:00");
        _draft.QuietHoursEnd = AppSettings.NormalizeTimeText(_draft.QuietHoursEnd, "08:00");

        foreach (var reminder in _draft.Reminders)
        {
            reminder.Name = string.IsNullOrWhiteSpace(reminder.Name) ? "提醒" : reminder.Name.Trim();
            reminder.IconKind = ReminderItem.NormalizeIconKind(reminder.IconKind, reminder.Id, reminder.Name);
            reminder.WorkMinutes = Math.Max(1, reminder.WorkMinutes);
            reminder.ActionMinutes = Math.Max(1, reminder.ActionMinutes);
        }
    }

    private static bool HaveRemindersChanged(
        IReadOnlyList<ReminderDefinition> current,
        IReadOnlyList<ReminderDefinition> draft)
    {
        if (current.Count != draft.Count)
        {
            return true;
        }

        for (var i = 0; i < current.Count; i++)
        {
            var left = current[i];
            var right = draft[i];
            if (left.Id != right.Id
                || left.Name != right.Name
                || left.IconKind != right.IconKind
                || left.WorkMinutes != right.WorkMinutes
                || left.ActionMinutes != right.ActionMinutes
                || left.IsEnabled != right.IsEnabled
                || left.SoundEnabled != right.SoundEnabled)
            {
                return true;
            }
        }

        return false;
    }

    private sealed record ScreenOption(string Name, string? DeviceName);
}
