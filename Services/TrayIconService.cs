using System.Drawing;
using System.Media;
using System.ComponentModel;
using System.Windows.Forms;
using WinDeskReminder.Models;

namespace WinDeskReminder.Services;

public sealed class TrayIconService : IDisposable
{
    private readonly AppSettings _settings;
    private readonly SettingsStore _settingsStore;
    private readonly DailyStatsStore _statsStore;
    private readonly ToastNotificationService _toastNotificationService;
    private readonly ReminderController _controller;
    private readonly MainWindow _widget;
    private readonly Action _openSettings;
    private readonly Action _exitApplication;
    private readonly NotifyIcon _notifyIcon;
    private readonly ToolStripMenuItem _showWidgetMenuItem;
    private readonly ToolStripMenuItem _widgetMenuItem;
    private readonly ToolStripMenuItem _pauseMenuItem;
    private readonly ToolStripMenuItem _soundMenuItem;
    private readonly ToolStripMenuItem _focusMenuItem;
    private readonly ToolStripMenuItem _statsMenuItem;
    private readonly ToolStripMenuItem _remindersMenuItem;

    public TrayIconService(
        AppSettings settings,
        SettingsStore settingsStore,
        DailyStatsStore statsStore,
        ToastNotificationService toastNotificationService,
        ReminderController controller,
        MainWindow widget,
        Action openSettings,
        Action exitApplication)
    {
        _settings = settings;
        _settingsStore = settingsStore;
        _statsStore = statsStore;
        _toastNotificationService = toastNotificationService;
        _controller = controller;
        _widget = widget;
        _openSettings = openSettings;
        _exitApplication = exitApplication;

        _widgetMenuItem = new ToolStripMenuItem("启用小挂件")
        {
            Checked = settings.WidgetEnabled,
            CheckOnClick = true
        };
        _widgetMenuItem.CheckedChanged += (_, _) =>
        {
            _settings.WidgetEnabled = _widgetMenuItem.Checked;
            _settingsStore.Save(_settings);
            _widget.ApplySettings();
            if (_settings.WidgetEnabled)
            {
                _widget.RevealTemporarily(TimeSpan.FromSeconds(6));
            }
        };

        _showWidgetMenuItem = new ToolStripMenuItem("显示小挂件");
        _showWidgetMenuItem.Click += (_, _) =>
        {
            ShowWidget(TimeSpan.FromSeconds(6));
        };

        _pauseMenuItem = new ToolStripMenuItem("暂停全部");
        _pauseMenuItem.Click += (_, _) =>
        {
            _controller.UserPaused = !_controller.UserPaused;
            RefreshMenu();
        };

        _soundMenuItem = new ToolStripMenuItem("提醒声音")
        {
            Checked = settings.SoundEnabled,
            CheckOnClick = true
        };
        _soundMenuItem.CheckedChanged += (_, _) =>
        {
            _settings.SoundEnabled = _soundMenuItem.Checked;
            _settingsStore.Save(_settings);
            RefreshMenu();
        };

        _focusMenuItem = new ToolStripMenuItem("专注模式");
        _statsMenuItem = new ToolStripMenuItem("今日完成");
        _remindersMenuItem = new ToolStripMenuItem("提醒项目");

        var settingsMenuItem = new ToolStripMenuItem("设置");
        settingsMenuItem.Click += (_, _) => _openSettings();

        var exitMenuItem = new ToolStripMenuItem("退出");
        exitMenuItem.Click += (_, _) => _exitApplication();

        _notifyIcon = new NotifyIcon
        {
            Icon = CreateTrayIcon(),
            Text = "WinDeskReminder",
            Visible = true,
            ContextMenuStrip = new ContextMenuStrip()
        };
        _notifyIcon.ContextMenuStrip.Opening += OnMenuOpening;
        _notifyIcon.ContextMenuStrip.Items.AddRange(
        [
            _showWidgetMenuItem,
            _widgetMenuItem,
            _pauseMenuItem,
            _soundMenuItem,
            _focusMenuItem,
            _statsMenuItem,
            _remindersMenuItem,
            new ToolStripSeparator(),
            settingsMenuItem,
            exitMenuItem
        ]);
        _controller.PropertyChanged += OnControllerPropertyChanged;
        _notifyIcon.BalloonTipClicked += (_, _) =>
        {
            ShowWidget(TimeSpan.FromSeconds(8));
        };
        _notifyIcon.DoubleClick += (_, _) =>
        {
            ShowWidget(TimeSpan.FromSeconds(6));
        };

        RefreshMenu();
    }

    public void RefreshMenu()
    {
        _widgetMenuItem.Checked = _settings.WidgetEnabled;
        _soundMenuItem.Checked = _settings.SoundEnabled;
        _pauseMenuItem.Text = _controller.UserPaused ? "恢复全部" : "暂停全部";
        BuildFocusMenu();
        BuildStatsMenu();
        BuildRemindersMenu();
    }

    public void ShowReminder(ReminderItem item)
    {
        if (_settings.SoundEnabled)
        {
            SystemSounds.Asterisk.Play();
        }

        _toastNotificationService.ShowReminder(item);

        if (_settings.WidgetEnabled)
        {
            ShowWidget(TimeSpan.FromSeconds(8));
        }
    }

    public void Dispose()
    {
        _controller.PropertyChanged -= OnControllerPropertyChanged;
        _notifyIcon.Visible = false;
        _notifyIcon.Icon?.Dispose();
        _notifyIcon.Dispose();
    }

    private static Icon CreateTrayIcon()
    {
        var bitmap = new Bitmap(32, 32);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        graphics.Clear(Color.Transparent);

        using var blueBrush = new SolidBrush(Color.FromArgb(37, 99, 235));
        using var whitePen = new Pen(Color.White, 2.4f)
        {
            StartCap = System.Drawing.Drawing2D.LineCap.Round,
            EndCap = System.Drawing.Drawing2D.LineCap.Round,
            LineJoin = System.Drawing.Drawing2D.LineJoin.Round
        };

        var heart = new System.Drawing.Drawing2D.GraphicsPath();
        heart.AddBezier(16, 10, 11, 3, 2, 7, 5, 16);
        heart.AddBezier(5, 16, 7, 23, 13, 26, 16, 29);
        heart.AddBezier(16, 29, 23, 26, 27, 20, 27, 16);
        heart.AddBezier(27, 16, 30, 7, 21, 3, 16, 10);
        graphics.FillPath(blueBrush, heart);

        graphics.DrawLines(
            whitePen,
            [
                new PointF(5, 17),
                new PointF(10, 17),
                new PointF(12.5f, 11),
                new PointF(16.5f, 24),
                new PointF(20, 14),
                new PointF(22.5f, 18),
                new PointF(27, 18)
            ]);

        var iconHandle = bitmap.GetHicon();
        var icon = (Icon)Icon.FromHandle(iconHandle).Clone();
        DestroyIcon(iconHandle);
        bitmap.Dispose();
        return icon;
    }

    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    private void OnMenuOpening(object? sender, CancelEventArgs e)
    {
        RefreshMenu();
    }

    private void OnControllerPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ReminderController.IsFocusModeActive) or nameof(ReminderController.FocusModeText))
        {
            RefreshMenu();
        }
    }

    private void ShowWidget(TimeSpan duration)
    {
        _settings.WidgetEnabled = true;
        _settingsStore.Save(_settings);
        _widgetMenuItem.Checked = true;
        _widget.Show();
        _widget.ApplySettings();
        _widget.RevealTemporarily(duration);
    }

    private void BuildFocusMenu()
    {
        _focusMenuItem.DropDownItems.Clear();
        _focusMenuItem.Text = _controller.IsFocusModeActive
            ? $"专注模式（{_controller.FocusModeText}）"
            : "专注模式";

        AddFocusDuration("25 分钟", TimeSpan.FromMinutes(25));
        AddFocusDuration("50 分钟", TimeSpan.FromMinutes(50));
        AddFocusDuration("90 分钟", TimeSpan.FromMinutes(90));

        _focusMenuItem.DropDownItems.Add(new ToolStripSeparator());
        var stopItem = new ToolStripMenuItem("结束专注模式")
        {
            Enabled = _controller.IsFocusModeActive
        };
        stopItem.Click += (_, _) =>
        {
            _controller.StopFocusMode();
            RefreshMenu();
        };
        _focusMenuItem.DropDownItems.Add(stopItem);
    }

    private void AddFocusDuration(string label, TimeSpan duration)
    {
        var item = new ToolStripMenuItem(label);
        item.Click += (_, _) =>
        {
            _controller.StartFocusMode(duration);
            RefreshMenu();
        };
        _focusMenuItem.DropDownItems.Add(item);
    }

    private void BuildRemindersMenu()
    {
        _remindersMenuItem.DropDownItems.Clear();

        foreach (var reminder in _controller.Items)
        {
            var reminderItem = new ToolStripMenuItem($"{reminder.Name}  {reminder.RemainingText}")
            {
                Enabled = reminder.IsEnabled
            };

            var resetItem = new ToolStripMenuItem("重置");
            resetItem.Click += (_, _) => _controller.Reset(reminder);
            reminderItem.DropDownItems.Add(resetItem);

            var snoozeItem = new ToolStripMenuItem("稍后 5 分钟");
            snoozeItem.Click += (_, _) => _controller.Snooze(reminder, TimeSpan.FromMinutes(5));
            reminderItem.DropDownItems.Add(snoozeItem);

            _remindersMenuItem.DropDownItems.Add(reminderItem);
        }

        if (_controller.Items.Count > 0)
        {
            _remindersMenuItem.DropDownItems.Add(new ToolStripSeparator());
        }

        var settingsItem = new ToolStripMenuItem("管理提醒项目");
        settingsItem.Click += (_, _) => _openSettings();
        _remindersMenuItem.DropDownItems.Add(settingsItem);
    }

    private void BuildStatsMenu()
    {
        _statsMenuItem.DropDownItems.Clear();
        var total = _statsStore.GetTodayTotal();
        _statsMenuItem.Text = total > 0 ? $"今日完成（{total}）" : "今日完成";

        var stats = _statsStore.GetTodayStats();
        if (stats.Count == 0)
        {
            _statsMenuItem.DropDownItems.Add(new ToolStripMenuItem("暂无完成记录")
            {
                Enabled = false
            });
            return;
        }

        foreach (var stat in stats)
        {
            _statsMenuItem.DropDownItems.Add(new ToolStripMenuItem($"{stat.ReminderName}：{stat.Count} 次")
            {
                Enabled = false
            });
        }
    }
}
