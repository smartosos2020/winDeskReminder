# WinDeskReminder

WinDeskReminder is a lightweight Windows desktop health reminder widget built with WPF. It docks to a screen edge, hides into a compact handle, and reminds you to stand, drink water, rest your eyes, or follow your own custom reminder schedule.

![WinDeskReminder UI](docs/images/windeskreminder-ui.png)

## Features

- Edge-docked floating widget with hover-to-expand behavior.
- Tray icon with show/hide, pause/resume, sound toggle, focus mode, settings, and exit commands.
- Multiple reminders with independent countdowns.
- Custom reminder add/delete support in Settings.
- Reminder confirmation flow: when a countdown reaches zero, confirm the reminder to start the action countdown, then automatically return to the work countdown.
- Focus mode presets for 25, 50, and 90 minutes.
- Timer pause while the session is locked, the system is suspended, focus mode is active, or the user has been idle for the configured duration.
- Select target display and dock edge.
- Drag the widget header to snap it to a nearby screen edge.
- Dynamic widget height based on the number of reminder items.
- Persistent settings stored at `%APPDATA%\WinDeskReminder\settings.json`.

## Requirements

- Windows 10/11 x64.
- .NET SDK 10.0 or later for development.

The published `win-x64` build is self-contained, so target machines do not need to install the .NET runtime.

## Run From Source

```powershell
dotnet run
```

## Build

```powershell
dotnet build .\WinDeskReminder.csproj -c Release
```

## Create Installer

The repository includes a PowerShell script that publishes a self-contained single-file executable and builds a single MSI package with WiX Toolset.

```powershell
.\scripts\Build-Installer.ps1
```

Output:

```text
artifacts\publish\win-x64-1.0.0-single\WinDeskReminder.exe
artifacts\installer\WinDeskReminder-1.0.0-x64.msi
```

To build another MSI version:

```powershell
.\scripts\Build-Installer.ps1 -Version 1.0.1
```

## Digital Signing

The generated MSI is not signed by default. For public distribution, sign both the published EXE and the MSI with a trusted code-signing certificate and timestamp server.

Example:

```powershell
signtool sign /fd SHA256 /tr http://timestamp.digicert.com /td SHA256 /f .\codesign.pfx /p <password> .\artifacts\publish\win-x64-1.0.0-single\WinDeskReminder.exe
signtool sign /fd SHA256 /tr http://timestamp.digicert.com /td SHA256 /f .\codesign.pfx /p <password> .\artifacts\installer\WinDeskReminder-1.0.0-x64.msi
```

## Project Structure

```text
Controls/                 Custom WPF controls
Models/                   Reminder and dock models
Services/                 Settings, tray icon, system activity, reminder controller
installer/Product.wxs     WiX MSI definition
scripts/Build-Installer.ps1
App.xaml / MainWindow.xaml / SettingsWindow.xaml
```

## Notes

- Build outputs, WiX local tools, installers, and debug symbols are intentionally excluded from Git.
- Settings and error logs are written under `%APPDATA%\WinDeskReminder`.
