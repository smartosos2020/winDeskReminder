using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Threading;
using WinDeskReminder.Models;
using WpfColor = System.Windows.Media.Color;

namespace WinDeskReminder.Services;

public sealed class ReminderController : INotifyPropertyChanged, IDisposable
{
    private readonly DispatcherTimer _timer;
    private readonly SystemActivityMonitor _activityMonitor = new();
    private AppSettings _settings;
    private bool _userPaused;
    private bool _systemPaused;
    private DateTimeOffset? _focusUntil;
    private string _systemStateText = "计时运行中";
    private static readonly WpfColor[] AccentPalette =
    [
        WpfColor.FromRgb(34, 197, 94),
        WpfColor.FromRgb(37, 99, 235),
        WpfColor.FromRgb(20, 184, 166),
        WpfColor.FromRgb(245, 158, 11),
        WpfColor.FromRgb(236, 72, 153),
        WpfColor.FromRgb(139, 92, 246),
        WpfColor.FromRgb(14, 165, 233),
        WpfColor.FromRgb(239, 68, 68)
    ];

    public ReminderController(AppSettings settings)
    {
        _settings = settings;
        Items = new ObservableCollection<ReminderItem>(settings.Reminders.Select(item => new ReminderItem(item)));
        AssignAccentColors();

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += OnTick;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action<ReminderItem>? NotificationRequested;
    public event Action<ReminderItem>? ReminderCompleted;

    public ObservableCollection<ReminderItem> Items { get; }

    public bool UserPaused
    {
        get => _userPaused;
        set
        {
            if (SetField(ref _userPaused, value))
            {
                UpdateSystemStateText();
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PauseActionText)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PauseActionGlyph)));
            }
        }
    }

    public string SystemStateText
    {
        get => _systemStateText;
        private set => SetField(ref _systemStateText, value);
    }

    public string PauseActionText => UserPaused ? "恢复" : "暂停";

    public string PauseActionGlyph => UserPaused ? "\uE768" : "\uE769";

    public bool IsFocusModeActive => _focusUntil is not null && _focusUntil > DateTimeOffset.Now;

    public string FocusModeText
    {
        get
        {
            if (!IsFocusModeActive || _focusUntil is null)
            {
                return "未开启";
            }

            var remaining = _focusUntil.Value - DateTimeOffset.Now;
            return remaining.TotalMinutes >= 1
                ? $"剩余 {Math.Ceiling(remaining.TotalMinutes):0} 分钟"
                : "即将结束";
        }
    }

    public void Start()
    {
        _timer.Start();
        UpdateSystemStateText();
    }

    public void ApplySettings(AppSettings settings)
    {
        _settings = settings;
        var existingById = Items
            .GroupBy(item => item.Id)
            .ToDictionary(group => group.Key, group => new Queue<ReminderItem>(group));
        var updatedItems = new List<ReminderItem>();

        foreach (var definition in settings.Reminders)
        {
            if (!existingById.TryGetValue(definition.Id, out var matches) || matches.Count == 0)
            {
                updatedItems.Add(new ReminderItem(definition));
                continue;
            }

            var item = matches.Dequeue();
            item.Name = definition.Name;
            item.IconKind = definition.IconKind;
            item.WorkMinutes = definition.WorkMinutes;
            item.ActionMinutes = definition.ActionMinutes;
            item.IsEnabled = definition.IsEnabled;
            item.SoundEnabled = definition.SoundEnabled;
            ClampRemainingToCurrentPhase(item);
            updatedItems.Add(item);
        }

        Items.Clear();
        foreach (var item in updatedItems)
        {
            Items.Add(item);
        }

        AssignAccentColors();

        UpdateSystemStateText();
    }

    public void StartFocusMode(TimeSpan duration)
    {
        _focusUntil = DateTimeOffset.Now.Add(duration);
        NotifyFocusChanged();
        UpdateSystemStateText();
    }

    public void StopFocusMode()
    {
        _focusUntil = null;
        NotifyFocusChanged();
        UpdateSystemStateText();
    }

    public void PrimaryAction(ReminderItem item)
    {
        if (!item.IsEnabled)
        {
            return;
        }

        if (item.Phase == ReminderPhase.AwaitingConfirmation)
        {
            item.Phase = ReminderPhase.Resting;
            item.Remaining = item.ActionDuration;
            return;
        }

        if (item.Phase == ReminderPhase.Resting)
        {
            CompleteReminder(item);
            return;
        }

        Reset(item);
    }

    public void Snooze(ReminderItem item, TimeSpan delay)
    {
        if (!item.IsEnabled)
        {
            return;
        }

        item.Phase = ReminderPhase.Working;
        item.Remaining = delay;
    }

    public void Reset(ReminderItem item)
    {
        item.Phase = ReminderPhase.Working;
        item.Remaining = item.WorkDuration;
    }

    public void Dispose()
    {
        _timer.Stop();
        _activityMonitor.Dispose();
    }

    private void OnTick(object? sender, EventArgs e)
    {
        if (_focusUntil is not null && _focusUntil <= DateTimeOffset.Now)
        {
            _focusUntil = null;
            NotifyFocusChanged();
        }

        _systemPaused = _activityMonitor.IsSystemPaused(TimeSpan.FromMinutes(_settings.IdlePauseMinutes));
        UpdateSystemStateText();

        if (UserPaused || _systemPaused || IsFocusModeActive || IsQuietHoursActive())
        {
            return;
        }

        foreach (var item in Items.Where(item => item.IsEnabled))
        {
            if (item.Phase == ReminderPhase.AwaitingConfirmation)
            {
                continue;
            }

            item.Remaining -= TimeSpan.FromSeconds(1);
            if (item.Remaining > TimeSpan.Zero)
            {
                continue;
            }

            if (item.Phase == ReminderPhase.Resting)
            {
                CompleteReminder(item);
            }
            else
            {
                item.Phase = ReminderPhase.AwaitingConfirmation;
                item.Remaining = TimeSpan.Zero;
                NotificationRequested?.Invoke(item);
            }
        }
    }

    private void UpdateSystemStateText()
    {
        if (IsFocusModeActive)
        {
            SystemStateText = $"专注模式：{FocusModeText}";
            return;
        }

        if (IsQuietHoursActive())
        {
            SystemStateText = $"勿扰时段：{_settings.QuietHoursStart}-{_settings.QuietHoursEnd}";
            return;
        }

        SystemStateText = UserPaused
            ? "已手动暂停"
            : _activityMonitor.GetPauseReason(TimeSpan.FromMinutes(_settings.IdlePauseMinutes));
    }

    private bool IsQuietHoursActive()
    {
        if (!_settings.QuietHoursEnabled
            || !TimeOnly.TryParse(_settings.QuietHoursStart, out var start)
            || !TimeOnly.TryParse(_settings.QuietHoursEnd, out var end))
        {
            return false;
        }

        if (start == end)
        {
            return true;
        }

        var now = TimeOnly.FromDateTime(DateTime.Now);
        return start < end
            ? now >= start && now < end
            : now >= start || now < end;
    }

    private void CompleteReminder(ReminderItem item)
    {
        item.Phase = ReminderPhase.Working;
        item.Remaining = item.WorkDuration;
        ReminderCompleted?.Invoke(item);
    }

    private void NotifyFocusChanged()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsFocusModeActive)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FocusModeText)));
    }

    private void AssignAccentColors()
    {
        for (var i = 0; i < Items.Count; i++)
        {
            Items[i].AccentColor = i < AccentPalette.Length
                ? AccentPalette[i]
                : ColorFromHsv((i * 137.508) % 360, 72, 82);
        }
    }

    private static void ClampRemainingToCurrentPhase(ReminderItem item)
    {
        var maxRemaining = item.Phase == ReminderPhase.Resting
            ? item.ActionDuration
            : item.WorkDuration;

        if (item.Remaining > maxRemaining)
        {
            item.Remaining = maxRemaining;
        }
    }

    private static WpfColor ColorFromHsv(double hue, double saturation, double value)
    {
        saturation /= 100;
        value /= 100;

        var chroma = value * saturation;
        var x = chroma * (1 - Math.Abs(hue / 60 % 2 - 1));
        var m = value - chroma;

        (double r, double g, double b) = hue switch
        {
            < 60 => (chroma, x, 0d),
            < 120 => (x, chroma, 0d),
            < 180 => (0d, chroma, x),
            < 240 => (0d, x, chroma),
            < 300 => (x, 0d, chroma),
            _ => (chroma, 0d, x)
        };

        return WpfColor.FromRgb(
            (byte)Math.Round((r + m) * 255),
            (byte)Math.Round((g + m) * 255),
            (byte)Math.Round((b + m) * 255));
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}
