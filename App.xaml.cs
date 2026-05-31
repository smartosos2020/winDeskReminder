using System.IO;
using System.Windows;
using System.Windows.Threading;
using WinDeskReminder.Services;

namespace WinDeskReminder;

public partial class App : System.Windows.Application
{
    private SettingsStore? _settingsStore;
    private AppSettings? _settings;
    private ReminderController? _controller;
    private MainWindow? _widget;
    private TrayIconService? _tray;
    private SettingsWindow? _settingsWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

        base.OnStartup(e);

        _settingsStore = new SettingsStore();
        _settings = _settingsStore.Load();
        _controller = new ReminderController(_settings);
        _widget = new MainWindow(_settings, _controller, _settingsStore, OpenSettings);
        _tray = new TrayIconService(_settings, _settingsStore, _controller, _widget, OpenSettings, ExitApplication);

        _controller.NotificationRequested += item => _tray.ShowReminder(item);
        _controller.Start();

        if (_settings.WidgetEnabled)
        {
            _widget.Show();
            _widget.RevealTemporarily(TimeSpan.FromSeconds(6));
        }
    }

    private void OpenSettings()
    {
        if (_settings is null || _settingsStore is null || _controller is null || _widget is null)
        {
            return;
        }

        if (_settingsWindow is not null)
        {
            _settingsWindow.Activate();
            return;
        }

        var settingsWindow = new SettingsWindow(_settings, _settingsStore, _controller, _widget)
        {
            Owner = _widget.IsVisible ? _widget : null
        };
        settingsWindow.Closed += (_, _) =>
        {
            if (ReferenceEquals(_settingsWindow, settingsWindow))
            {
                _settingsWindow = null;
            }
        };
        _settingsWindow = settingsWindow;
        settingsWindow.Show();
        settingsWindow.Activate();
    }

    private void ExitApplication()
    {
        _tray?.Dispose();
        _controller?.Dispose();
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _tray?.Dispose();
        _controller?.Dispose();
        base.OnExit(e);
    }

    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        LogException(e.Exception);
        System.Windows.MessageBox.Show(
            $"WinDeskReminder 启动或运行时出错：{e.Exception.Message}\n\n日志已写入：{GetLogPath()}",
            "WinDeskReminder",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        e.Handled = true;
        Current.Shutdown(-1);
    }

    private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception exception)
        {
            LogException(exception);
        }
    }

    private static void LogException(Exception exception)
    {
        try
        {
            var path = GetLogPath();
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.AppendAllText(path, $"[{DateTimeOffset.Now:O}]{Environment.NewLine}{exception}{Environment.NewLine}{Environment.NewLine}");
        }
        catch
        {
            // Logging must not create a second startup failure.
        }
    }

    private static string GetLogPath()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "WinDeskReminder",
            "error.log");
    }
}
