using System.ComponentModel;
using System.Runtime.CompilerServices;
using WpfBrush = System.Windows.Media.Brush;
using WpfColor = System.Windows.Media.Color;
using WpfSolidColorBrush = System.Windows.Media.SolidColorBrush;

namespace WinDeskReminder.Models;

public sealed class ReminderItem : INotifyPropertyChanged
{
    private string _name;
    private int _workMinutes;
    private int _actionMinutes;
    private bool _isEnabled;
    private WpfColor _accentColor = WpfColor.FromRgb(34, 197, 94);
    private ReminderPhase _phase = ReminderPhase.Working;
    private TimeSpan _remaining;

    public ReminderItem(ReminderDefinition definition)
    {
        Id = definition.Id;
        _name = definition.Name;
        _workMinutes = Math.Max(1, definition.WorkMinutes);
        _actionMinutes = Math.Max(1, definition.ActionMinutes);
        _isEnabled = definition.IsEnabled;
        _remaining = WorkDuration;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Id { get; }

    public string Name
    {
        get => _name;
        set
        {
            if (SetField(ref _name, value))
            {
                OnPropertyChanged(nameof(IconKind));
            }
        }
    }

    public int WorkMinutes
    {
        get => _workMinutes;
        set
        {
            if (SetField(ref _workMinutes, Math.Max(1, value)))
            {
                OnPropertyChanged(nameof(WorkDuration));
                OnPropertyChanged(nameof(ProgressPercent));
            }
        }
    }

    public int ActionMinutes
    {
        get => _actionMinutes;
        set
        {
            if (SetField(ref _actionMinutes, Math.Max(1, value)))
            {
                OnPropertyChanged(nameof(ActionDuration));
                OnPropertyChanged(nameof(ProgressPercent));
            }
        }
    }

    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (SetField(ref _isEnabled, value))
            {
                OnPropertyChanged(nameof(PhaseText));
            }
        }
    }

    public WpfColor AccentColor
    {
        get => _accentColor;
        set
        {
            if (SetField(ref _accentColor, value))
            {
                OnPropertyChanged(nameof(AccentBrush));
                OnPropertyChanged(nameof(AccentSoftBrush));
            }
        }
    }

    public WpfBrush AccentBrush => new WpfSolidColorBrush(AccentColor);

    public WpfBrush AccentSoftBrush => new WpfSolidColorBrush(WpfColor.FromArgb(
        30,
        AccentColor.R,
        AccentColor.G,
        AccentColor.B));

    public ReminderPhase Phase
    {
        get => _phase;
        set
        {
            if (SetField(ref _phase, value))
            {
                OnPropertyChanged(nameof(PhaseText));
                OnPropertyChanged(nameof(PrimaryActionText));
                OnPropertyChanged(nameof(PrimaryActionGlyph));
                OnPropertyChanged(nameof(ProgressPercent));
            }
        }
    }

    public TimeSpan Remaining
    {
        get => _remaining;
        set
        {
            if (SetField(ref _remaining, value < TimeSpan.Zero ? TimeSpan.Zero : value))
            {
                OnPropertyChanged(nameof(RemainingText));
                OnPropertyChanged(nameof(ProgressPercent));
            }
        }
    }

    public TimeSpan WorkDuration => TimeSpan.FromMinutes(WorkMinutes);
    public TimeSpan ActionDuration => TimeSpan.FromMinutes(ActionMinutes);

    public string RemainingText
    {
        get
        {
            var totalSeconds = Math.Max(0, (int)Math.Ceiling(Remaining.TotalSeconds));
            var minutes = totalSeconds / 60;
            var seconds = totalSeconds % 60;
            return $"{minutes:00}:{seconds:00}";
        }
    }

    public string PhaseText
    {
        get
        {
            if (!IsEnabled)
            {
                return "已停用";
            }

            return Phase switch
            {
                ReminderPhase.AwaitingConfirmation => "待确认",
                ReminderPhase.Resting => "执行中",
                _ => "倒计时"
            };
        }
    }

    public string PrimaryActionText => Phase switch
    {
        ReminderPhase.AwaitingConfirmation => "开始",
        ReminderPhase.Resting => "完成",
        _ => "跳过"
    };

    public string PrimaryActionGlyph => Phase switch
    {
        ReminderPhase.AwaitingConfirmation => "\uE768",
        ReminderPhase.Resting => "\uE73E",
        _ => "\uE893"
    };

    public string IconKind
    {
        get
        {
            var key = $"{Id} {Name}".ToLowerInvariant();
            if (key.Contains("water") || key.Contains("喝水"))
            {
                return "water";
            }

            if (key.Contains("rest") || key.Contains("eye") || key.Contains("休息") || key.Contains("眼"))
            {
                return "eye";
            }

            return "stand";
        }
    }

    public double ProgressPercent
    {
        get
        {
            var total = Phase == ReminderPhase.Resting ? ActionDuration.TotalSeconds : WorkDuration.TotalSeconds;
            if (total <= 0)
            {
                return 0;
            }

            var elapsed = total - Remaining.TotalSeconds;
            return Math.Clamp(elapsed / total * 100, 0, 100);
        }
    }

    public ReminderDefinition ToDefinition()
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

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
