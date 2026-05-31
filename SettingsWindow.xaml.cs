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
            WorkMinutes = 30,
            ActionMinutes = 5,
            IsEnabled = true
        });

        ReminderGrid.Items.Refresh();
        ReminderGrid.SelectedIndex = _draft.Reminders.Count - 1;
    }

    private void DeleteReminder_Click(object sender, RoutedEventArgs e)
    {
        ReminderGrid.CommitEdit(DataGridEditingUnit.Cell, true);
        ReminderGrid.CommitEdit(DataGridEditingUnit.Row, true);

        var selectedItems = ReminderGrid.SelectedItems
            .OfType<ReminderDefinition>()
            .ToList();

        foreach (var selected in selectedItems)
        {
            _draft.Reminders.Remove(selected);
        }

        if (_draft.Reminders.Count == 0)
        {
            _draft.Reminders.Add(new ReminderDefinition
            {
                Name = "新提醒",
                WorkMinutes = 30,
                ActionMinutes = 5,
                IsEnabled = true
            });
        }

        ReminderGrid.Items.Refresh();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        ReminderGrid.CommitEdit(DataGridEditingUnit.Cell, true);
        ReminderGrid.CommitEdit(DataGridEditingUnit.Row, true);

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

        foreach (var reminder in _draft.Reminders)
        {
            reminder.Name = string.IsNullOrWhiteSpace(reminder.Name) ? "提醒" : reminder.Name.Trim();
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
                || left.WorkMinutes != right.WorkMinutes
                || left.ActionMinutes != right.ActionMinutes
                || left.IsEnabled != right.IsEnabled)
            {
                return true;
            }
        }

        return false;
    }

    private sealed record ScreenOption(string Name, string? DeviceName);
}
