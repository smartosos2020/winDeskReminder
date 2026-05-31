using System.IO;
using System.Runtime.InteropServices;
using System.Security;
using System.Windows;
using Microsoft.Win32;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;
using WinDeskReminder.Models;

namespace WinDeskReminder.Services;

public sealed class ToastNotificationService
{
    public const string AppUserModelId = "WinDeskReminder.App";
    private const string ProtocolName = "windeskreminder";

    public ToastNotificationService()
    {
        EnsureProtocolRegistration();
        EnsureStartMenuShortcut();
    }

    public void ShowReminder(ReminderItem item)
    {
        try
        {
            var document = new XmlDocument();
            document.LoadXml(BuildToastXml(item));
            ToastNotificationManager.CreateToastNotifier(AppUserModelId).Show(new ToastNotification(document));
        }
        catch
        {
            // Toast support can be disabled by policy or unavailable in some sessions.
        }
    }

    private static string BuildToastXml(ReminderItem item)
    {
        var id = Uri.EscapeDataString(item.Id);
        var title = SecurityElement.Escape(item.Name);
        return $"""
            <toast launch="windeskreminder://show">
              <visual>
                <binding template="ToastGeneric">
                  <text>{title}</text>
                  <text>提醒时间到了。确认后开始执行倒计时。</text>
                </binding>
              </visual>
              <actions>
                <action content="开始执行" activationType="protocol" arguments="windeskreminder://reminder/{id}/start" />
                <action content="稍后 5 分钟" activationType="protocol" arguments="windeskreminder://reminder/{id}/snooze" />
                <action content="跳过" activationType="protocol" arguments="windeskreminder://reminder/{id}/skip" />
              </actions>
            </toast>
            """;
    }

    private static void EnsureProtocolRegistration()
    {
        using var root = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{ProtocolName}");
        root.SetValue(null, "URL:WinDeskReminder Protocol");
        root.SetValue("URL Protocol", string.Empty);

        using var command = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{ProtocolName}\shell\open\command");
        command.SetValue(null, $"\"{StartupService.GetExecutablePath()}\" \"%1\"");
    }

    private static void EnsureStartMenuShortcut()
    {
        try
        {
            var programs = Environment.GetFolderPath(Environment.SpecialFolder.StartMenu);
            var shortcutPath = Path.Combine(programs, "Programs", "WinDeskReminder.lnk");
            Directory.CreateDirectory(Path.GetDirectoryName(shortcutPath)!);

            var shellLinkType = Type.GetTypeFromCLSID(new Guid("00021401-0000-0000-C000-000000000046"));
            if (shellLinkType is null)
            {
                return;
            }

            var shellLink = (IShellLinkW)Activator.CreateInstance(shellLinkType)!;
            shellLink.SetPath(StartupService.GetExecutablePath());
            shellLink.SetWorkingDirectory(AppContext.BaseDirectory);
            shellLink.SetDescription("WinDeskReminder");

            using var appId = PropVariant.FromString(AppUserModelId);
            var propertyStore = (IPropertyStore)shellLink;
            var appUserModelIdKey = AppUserModelIdKey;
            propertyStore.SetValue(ref appUserModelIdKey, appId.Value);
            propertyStore.Commit();

            var persistFile = (IPersistFile)shellLink;
            persistFile.Save(shortcutPath, true);
        }
        catch
        {
            // The app still works without a shortcut; toast display depends on OS policy.
        }
    }

    private static readonly PropertyKey AppUserModelIdKey = new()
    {
        FormatId = new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"),
        PropertyId = 5
    };

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("000214F9-0000-0000-C000-000000000046")]
    private interface IShellLinkW
    {
        void GetPath(IntPtr pszFile, int cchMaxPath, IntPtr pfd, uint fFlags);
        void GetIDList(out IntPtr ppidl);
        void SetIDList(IntPtr pidl);
        void GetDescription(IntPtr pszName, int cchMaxName);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
        void GetWorkingDirectory(IntPtr pszDir, int cchMaxPath);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
        void GetArguments(IntPtr pszArgs, int cchMaxPath);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
        void GetHotkey(out short pwHotkey);
        void SetHotkey(short wHotkey);
        void GetShowCmd(out int piShowCmd);
        void SetShowCmd(int iShowCmd);
        void GetIconLocation(IntPtr pszIconPath, int cchIconPath, out int piIcon);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, uint dwReserved);
        void Resolve(IntPtr hwnd, uint fFlags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("0000010b-0000-0000-C000-000000000046")]
    private interface IPersistFile
    {
        void GetClassID(out Guid pClassID);
        void IsDirty();
        void Load([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, uint dwMode);
        void Save([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, bool fRemember);
        void SaveCompleted([MarshalAs(UnmanagedType.LPWStr)] string pszFileName);
        void GetCurFile([MarshalAs(UnmanagedType.LPWStr)] out string ppszFileName);
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("00000138-0000-0000-C000-000000000046")]
    private interface IPropertyStore
    {
        void GetCount(out uint propertyCount);
        void GetAt(uint propertyIndex, out PropertyKey key);
        void GetValue(ref PropertyKey key, out PropVariantValue value);
        void SetValue(ref PropertyKey key, PropVariantValue value);
        void Commit();
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    private struct PropertyKey
    {
        public Guid FormatId;
        public uint PropertyId;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PropVariantValue
    {
        public ushort VariantType;
        public ushort Reserved1;
        public ushort Reserved2;
        public ushort Reserved3;
        public IntPtr Value;
        public IntPtr Value2;
    }

    private sealed class PropVariant : IDisposable
    {
        private PropVariant(PropVariantValue value)
        {
            Value = value;
        }

        public PropVariantValue Value { get; private set; }

        public static PropVariant FromString(string value)
        {
            return new PropVariant(new PropVariantValue
            {
                VariantType = 31,
                Value = Marshal.StringToCoTaskMemUni(value)
            });
        }

        public void Dispose()
        {
            if (Value.Value != IntPtr.Zero)
            {
                Marshal.FreeCoTaskMem(Value.Value);
                Value = default;
            }
        }
    }
}
