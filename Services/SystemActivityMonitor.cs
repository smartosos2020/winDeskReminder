using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace WinDeskReminder.Services;

public sealed class SystemActivityMonitor : IDisposable
{
    private bool _isLocked;
    private bool _isSuspended;

    public SystemActivityMonitor()
    {
        SystemEvents.SessionSwitch += OnSessionSwitch;
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
    }

    public bool IsSystemPaused(TimeSpan idleThreshold)
    {
        return _isLocked || _isSuspended || GetIdleTime() >= idleThreshold;
    }

    public string GetPauseReason(TimeSpan idleThreshold)
    {
        if (_isLocked)
        {
            return "已锁屏，计时暂停";
        }

        if (_isSuspended)
        {
            return "系统睡眠中，计时暂停";
        }

        return GetIdleTime() >= idleThreshold ? "长时间无操作，计时暂停" : "计时运行中";
    }

    public void Dispose()
    {
        SystemEvents.SessionSwitch -= OnSessionSwitch;
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
    }

    private void OnSessionSwitch(object sender, SessionSwitchEventArgs e)
    {
        if (e.Reason == SessionSwitchReason.SessionLock)
        {
            _isLocked = true;
        }
        else if (e.Reason == SessionSwitchReason.SessionUnlock)
        {
            _isLocked = false;
        }
    }

    private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Suspend)
        {
            _isSuspended = true;
        }
        else if (e.Mode == PowerModes.Resume)
        {
            _isSuspended = false;
        }
    }

    private static TimeSpan GetIdleTime()
    {
        var info = new LastInputInfo
        {
            Size = (uint)Marshal.SizeOf<LastInputInfo>()
        };

        if (!GetLastInputInfo(ref info))
        {
            return TimeSpan.Zero;
        }

        var currentTick = unchecked((uint)GetTickCount64());
        var idleMilliseconds = unchecked(currentTick - info.Time);
        return TimeSpan.FromMilliseconds(idleMilliseconds);
    }

    [DllImport("user32.dll")]
    private static extern bool GetLastInputInfo(ref LastInputInfo plii);

    [DllImport("kernel32.dll")]
    private static extern ulong GetTickCount64();

    [StructLayout(LayoutKind.Sequential)]
    private struct LastInputInfo
    {
        public uint Size;
        public uint Time;
    }
}
