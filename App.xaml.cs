using System.Windows;
using System.Windows.Threading;
using WinDeskReminder.Services;

namespace WinDeskReminder;

public partial class App : System.Windows.Application
{
    private AppActivationService? _activationService;
    private SettingsStore? _settingsStore;
    private DailyStatsStore? _statsStore;
    private AppSettings? _settings;
    private ReminderController? _controller;
    private MainWindow? _widget;
    private TrayIconService? _tray;
    private SettingsWindow? _settingsWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

        _activationService = new AppActivationService();
        if (!_activationService.IsPrimaryInstance)
        {
            AppActivationService.TrySendToPrimary(e.Args);
            Shutdown();
            return;
        }

        base.OnStartup(e);

        _settingsStore = new SettingsStore();
        _statsStore = new DailyStatsStore();
        _settings = _settingsStore.Load();
        StartupService.Apply(_settings.StartWithWindows);

        _controller = new ReminderController(_settings);
        _widget = new MainWindow(_settings, _controller, _settingsStore, OpenSettings);
        var toastNotifications = new ToastNotificationService();
        var updateService = new UpdateService();
        _tray = new TrayIconService(
            _settings,
            _settingsStore,
            _statsStore,
            toastNotifications,
            updateService,
            _controller,
            _widget,
            OpenSettings,
            ExitApplication);

        _controller.NotificationRequested += item => _tray.ShowReminder(item);
        _controller.ReminderCompleted += item =>
        {
            _statsStore.RecordCompletion(item);
            _tray.RefreshMenu();
        };
        _activationService.CommandReceived += HandleActivationCommand;
        _activationService.Start();
        _controller.Start();

        if (_settings.WidgetEnabled)
        {
            _widget.Show();
            _widget.RevealTemporarily(TimeSpan.FromSeconds(6));
        }

        _activationService.Dispatch(e.Args);
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
        _activationService?.Dispose();
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _tray?.Dispose();
        _controller?.Dispose();
        _activationService?.Dispose();
        base.OnExit(e);
    }

    private void HandleActivationCommand(string command)
    {
        if (_controller is null || _widget is null)
        {
            return;
        }

        if (!Uri.TryCreate(command.Trim('"'), UriKind.Absolute, out var uri)
            || !string.Equals(uri.Scheme, "windeskreminder", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (uri.Host.Equals("show", StringComparison.OrdinalIgnoreCase))
        {
            _widget.Show();
            _widget.RevealTemporarily(TimeSpan.FromSeconds(8));
            return;
        }

        var parts = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (!uri.Host.Equals("reminder", StringComparison.OrdinalIgnoreCase) || parts.Length != 2)
        {
            return;
        }

        var reminderId = Uri.UnescapeDataString(parts[0]);
        var action = parts[1];
        var item = _controller.Items.FirstOrDefault(entry => entry.Id == reminderId);
        if (item is null)
        {
            return;
        }

        switch (action)
        {
            case "start":
                _controller.PrimaryAction(item);
                break;
            case "snooze":
                _controller.Snooze(item, TimeSpan.FromMinutes(5));
                break;
            case "skip":
                _controller.Reset(item);
                break;
        }

        _widget.Show();
        _widget.RevealTemporarily(TimeSpan.FromSeconds(6));
    }

    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        LogException(e.Exception);
        System.Windows.MessageBox.Show(
            $"WinDeskReminder 启动或运行时出错：{e.Exception.Message}\n\n日志已写入：{AppPaths.ErrorLogPath}",
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
        LogService.Write(exception);
    }
}
