using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using WinDeskReminder.Controls;
using WinDeskReminder.Models;
using WinDeskReminder.Services;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using MediaColor = System.Windows.Media.Color;
using DrawingPoint = System.Drawing.Point;
using WpfButton = System.Windows.Controls.Button;
using WpfPoint = System.Windows.Point;

namespace WinDeskReminder;

public partial class MainWindow : Window
{
    private const double ExpandedWidth = 286;
    private const double ExpandedBaseHeight = 96;
    private const double ReminderItemHeight = 54;
    private const double EdgeContactTolerance = 2;
    private readonly AppSettings _settings;
    private readonly ReminderController _controller;
    private readonly SettingsStore _settingsStore;
    private readonly Action _openSettings;
    private readonly DispatcherTimer _hideTimer;
    private bool _isExpanded;
    private bool _isDragging;

    public MainWindow(
        AppSettings settings,
        ReminderController controller,
        SettingsStore settingsStore,
        Action openSettings)
    {
        _settings = settings;
        _controller = controller;
        _settingsStore = settingsStore;
        _openSettings = openSettings;

        InitializeComponent();
        DataContext = controller;

        _hideTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(650) };
        _hideTimer.Tick += (_, _) =>
        {
            _hideTimer.Stop();
            CollapseToEdge();
        };

        ApplyChrome();
    }

    public void ApplySettings()
    {
        ApplyChrome();

        if (_settings.WidgetEnabled)
        {
            if (!IsVisible)
            {
                Show();
            }

            if (_isExpanded || !_settings.IsDocked)
            {
                PositionWindow(expanded: true);
            }
            else
            {
                CollapseToEdge();
            }
        }
        else
        {
            Hide();
        }
    }

    private void ApplyChrome()
    {
        Shell.Background = new SolidColorBrush(MediaColor.FromArgb(
            255,
            248,
            250,
            252));

        Opacity = Math.Clamp(_settings.BackdropOpacity, 0.25, 1);
        CollapsedHandle.DockEdge = _settings.DockEdge;
        ApplyWindowMode(_isExpanded || !_settings.IsDocked);
    }

    public void ExpandFromEdge()
    {
        _hideTimer.Stop();
        _isExpanded = true;
        PositionWindow(expanded: true);
        Activate();
    }

    public void RevealTemporarily(TimeSpan duration)
    {
        if (!_settings.WidgetEnabled)
        {
            return;
        }

        if (!IsVisible)
        {
            Show();
        }

        ExpandFromEdge();
        _hideTimer.Stop();
        _hideTimer.Interval = duration;
        _hideTimer.Start();
    }

    private void CollapseToEdge()
    {
        if (!_settings.IsDocked)
        {
            return;
        }

        _hideTimer.Interval = TimeSpan.FromMilliseconds(650);
        _isExpanded = false;
        PositionWindow(expanded: false);
    }

    private void PositionWindow(bool expanded)
    {
        ApplyWindowMode(expanded);
        var screen = ResolveScreen();
        var area = GetWorkingAreaInDips(screen);

        if (!_settings.IsDocked)
        {
            return;
        }

        if (expanded)
        {
            switch (_settings.DockEdge)
            {
                case DockEdge.Left:
                    Left = area.Left;
                    Top = GetOffsetPosition(area.Top, area.Height, Height);
                    break;
                case DockEdge.Top:
                    Left = GetOffsetPosition(area.Left, area.Width, Width);
                    Top = area.Top;
                    break;
                case DockEdge.Bottom:
                    Left = GetOffsetPosition(area.Left, area.Width, Width);
                    Top = area.Bottom - Height;
                    break;
                default:
                    Left = area.Right - Width;
                    Top = GetOffsetPosition(area.Top, area.Height, Height);
                    break;
            }

            return;
        }

        switch (_settings.DockEdge)
        {
            case DockEdge.Left:
                Left = area.Left;
                Top = GetOffsetPosition(area.Top, area.Height, Height);
                break;
            case DockEdge.Top:
                Left = GetOffsetPosition(area.Left, area.Width, Width);
                Top = area.Top;
                break;
            case DockEdge.Bottom:
                Left = GetOffsetPosition(area.Left, area.Width, Width);
                Top = area.Bottom - Height;
                break;
            default:
                Left = area.Right - Width;
                Top = GetOffsetPosition(area.Top, area.Height, Height);
                break;
        }
    }

    private double GetOffsetPosition(double start, double length, double ownLength)
    {
        var travel = Math.Max(0, length - ownLength);
        return start + travel * Math.Clamp(_settings.DockOffsetRatio, 0, 1);
    }

    private void ApplyWindowMode(bool expanded)
    {
        if (expanded)
        {
            Width = ExpandedWidth;
            Height = CalculateExpandedHeight();
            Shell.Visibility = Visibility.Visible;
            CollapsedHandle.Visibility = Visibility.Collapsed;
            return;
        }

        var handleSize = SegmentedHandle.DesiredSizeForEdge(_settings.DockEdge);
        Width = handleSize.Width;
        Height = handleSize.Height;
        Shell.Visibility = Visibility.Collapsed;
        CollapsedHandle.DockEdge = _settings.DockEdge;
        CollapsedHandle.Visibility = Visibility.Visible;
        CollapsedHandle.InvalidateVisual();
    }

    private void SnapToNearestEdge()
    {
        var center = DipPointToDevice(new WpfPoint(Left + ActualWidth / 2, Top + ActualHeight / 2));
        var screen = Screen.FromPoint(center);
        var area = GetWorkingAreaInDips(screen);
        var scoreToLeft = GetEdgeSnapScore(area.Left - Left);
        var scoreToRight = GetEdgeSnapScore((Left + ActualWidth) - area.Right);
        var scoreToTop = GetEdgeSnapScore(area.Top - Top);
        var scoreToBottom = GetEdgeSnapScore((Top + ActualHeight) - area.Bottom);
        var maxScore = new[] { scoreToLeft, scoreToRight, scoreToTop, scoreToBottom }.Max();

        if (maxScore <= 0)
        {
            _settings.IsDocked = false;
            _settings.ScreenDeviceName = screen.DeviceName;
            _settingsStore.Save(_settings);
            _isExpanded = true;
            ApplyWindowMode(expanded: true);
            _hideTimer.Stop();
            return;
        }

        _settings.IsDocked = true;
        _settings.ScreenDeviceName = screen.DeviceName;
        _settings.DockEdge = maxScore == scoreToLeft
            ? DockEdge.Left
            : maxScore == scoreToRight
                ? DockEdge.Right
                : maxScore == scoreToTop
                    ? DockEdge.Top
                    : DockEdge.Bottom;

        var expandedHeight = CalculateExpandedHeight();
        _settings.DockOffsetRatio = _settings.DockEdge is DockEdge.Left or DockEdge.Right
            ? CalculateOffsetRatio(Top, area.Top, area.Height, expandedHeight)
            : CalculateOffsetRatio(Left, area.Left, area.Width, ExpandedWidth);

        _settingsStore.Save(_settings);
        ApplyChrome();
        ExpandFromEdge();

        _hideTimer.Stop();
        _hideTimer.Interval = TimeSpan.FromSeconds(2);
        _hideTimer.Start();
    }

    private static double GetEdgeSnapScore(double overlapOrContact)
    {
        if (overlapOrContact > 0)
        {
            return 100 + overlapOrContact;
        }

        var distance = Math.Abs(overlapOrContact);
        return distance <= EdgeContactTolerance ? EdgeContactTolerance - distance + 1 : 0;
    }

    private static double CalculateOffsetRatio(double currentStart, double areaStart, double areaLength, double ownLength)
    {
        var travel = Math.Max(1, areaLength - ownLength);
        return Math.Clamp((currentStart - areaStart) / travel, 0, 1);
    }

    private double CalculateExpandedHeight()
    {
        var reminderCount = Math.Max(1, _controller.Items.Count);
        return ExpandedBaseHeight + reminderCount * ReminderItemHeight;
    }

    private Screen ResolveScreen()
    {
        if (!string.IsNullOrWhiteSpace(_settings.ScreenDeviceName))
        {
            var selected = Screen.AllScreens.FirstOrDefault(screen => screen.DeviceName == _settings.ScreenDeviceName);
            if (selected is not null)
            {
                return selected;
            }
        }

        return Screen.PrimaryScreen ?? Screen.AllScreens.First();
    }

    private Rect GetWorkingAreaInDips(Screen screen)
    {
        var transform = PresentationSource.FromVisual(this)?.CompositionTarget?.TransformFromDevice ?? Matrix.Identity;
        var topLeft = transform.Transform(new WpfPoint(screen.WorkingArea.Left, screen.WorkingArea.Top));
        var bottomRight = transform.Transform(new WpfPoint(screen.WorkingArea.Right, screen.WorkingArea.Bottom));
        return new Rect(topLeft, bottomRight);
    }

    private DrawingPoint DipPointToDevice(WpfPoint point)
    {
        var transform = PresentationSource.FromVisual(this)?.CompositionTarget?.TransformToDevice ?? Matrix.Identity;
        var devicePoint = transform.Transform(point);
        return new DrawingPoint((int)Math.Round(devicePoint.X), (int)Math.Round(devicePoint.Y));
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        ApplySettings();
    }

    private void Window_MouseEnter(object sender, MouseEventArgs e)
    {
        _hideTimer.Stop();
        if (!_isExpanded)
        {
            ExpandFromEdge();
        }
    }

    private void Window_MouseLeave(object sender, MouseEventArgs e)
    {
        if (_isDragging)
        {
            return;
        }

        _hideTimer.Stop();
        if (_settings.IsDocked)
        {
            _hideTimer.Start();
        }
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (FindVisualParent<WpfButton>(e.OriginalSource as DependencyObject) is not null)
        {
            return;
        }

        _hideTimer.Stop();
        _isDragging = true;

        try
        {
            DragMove();
            SnapToNearestEdge();
        }
        finally
        {
            _isDragging = false;
        }
    }

    private void PrimaryAction_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is ReminderItem item)
        {
            _controller.PrimaryAction(item);
        }
    }

    private void Snooze_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is ReminderItem item)
        {
            _controller.Snooze(item, TimeSpan.FromMinutes(5));
        }
    }

    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is ReminderItem item)
        {
            _controller.Reset(item);
        }
    }

    private void PauseAll_Click(object sender, RoutedEventArgs e)
    {
        _controller.UserPaused = !_controller.UserPaused;
    }

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        _openSettings();
    }

    private static T? FindVisualParent<T>(DependencyObject? current)
        where T : DependencyObject
    {
        while (current is not null)
        {
            if (current is T match)
            {
                return match;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }
}
