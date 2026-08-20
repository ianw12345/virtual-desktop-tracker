# Virtual Desktop Tracker

Virtual Desktop Tracker is a single Windows desktop application for showing the active virtual desktop, recording how long it is used, and analysing that history. The executable is **VirtualDesktopDisplayer**; there is no separate command-line tracker.

## Features

- Always-on-top desktop-name display, available across virtual desktops
- Automatic usage tracking, including a `Screen Off` state when Windows is locked or the display is unavailable
- Rename, create, and jump to virtual desktops from the right-click menu
- Date-selectable working-hours estimation
- Date-selectable text and JSON reports with configurable activity consolidation
- Timeline, project detection, issue tracking, and Timely upload integration
- Tracking status and direct access to the current log file/folder
- Optional global Back/Forward mouse buttons for previous/next desktop navigation

## Project layout

```
virtual-desktop-tracker/
├── VirtualDesktopDisplayer/       # The single WinForms application
├── VirtualDesktopHelper/          # Shared tracking, analysis, and integration services
├── VirtualDesktopHelper.Tests/    # Automated tests
├── VirtualDesktop/                # Locally compiled MScholtes helper executables (not committed)
├── run-displayer.bat              # Development launcher
└── Run-VirtualDesktopDisplayer.bat # Release launcher
```

## Requirements

- Windows 10 or 11 with virtual desktops
- .NET SDK 9.0 or later
- The helper executables from [MScholtes/VirtualDesktop](https://github.com/MScholtes/VirtualDesktop)

The display is pinned to all virtual desktops with `VirtualDesktop11-24H2.exe` (preferred) or `VirtualDesktop11.exe`. When the helpers are present in the repository's `VirtualDesktop` directory, the build copies them beside `VirtualDesktopDisplayer.exe` automatically.

### Build the VirtualDesktop helper

From the repository root:

```powershell
git clone https://github.com/MScholtes/VirtualDesktop.git VirtualDesktop
& 'C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe' .\VirtualDesktop\VirtualDesktop11.cs /out:.\VirtualDesktop\VirtualDesktop11.exe /win32icon:.\VirtualDesktop\VirtualDesktop.ico
& 'C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe' .\VirtualDesktop\VirtualDesktop11-24H2.cs /out:.\VirtualDesktop\VirtualDesktop11-24H2.exe /win32icon:.\VirtualDesktop\VirtualDesktop.ico
```

Use `VirtualDesktop11.exe` instead on Windows versions for which the 24H2 helper is not compatible. GitHub Actions builds both helpers automatically to verify the solution.

## Build and start

```powershell
dotnet restore .\virtual-desktop-tracker.sln
dotnet build .\virtual-desktop-tracker.sln --configuration Release
.\VirtualDesktopDisplayer\bin\Release\net9.0-windows\VirtualDesktopDisplayer.exe
```

For development, run `.\run-displayer.bat`. For a built release, use `.\Run-VirtualDesktopDisplayer.bat`.

## Using the app

1. Start `VirtualDesktopDisplayer.exe`. The active desktop name appears at the bottom-right corner.
2. Right-click the display to open the menu. Double-click it to exit.
3. Use **Rename** options to name a desktop after the task or issue you are working on.
4. Open **Extras** for analysis and diagnostics:
   - **Working Hours...** calculates total time, remaining time, lunch break, and an estimated finish time for a selected date.
   - **Generate Report** creates text and JSON reports for a selected date. Its dialog exposes the former CLI options: activity consolidation, minimum duration, and maximum custom duration.
   - **Tracking Status** shows the current desktop, most recent poll, intervals, log location, and last tracking error.
   - **View Log JSON** and **Open Log Folder** provide direct access to persisted data.
5. Use **Configure** to set up Timely, project mappings, issue tracking, and mouse navigation.
   - Enable **Use mouse Back/Forward buttons to switch desktops** to map the dedicated Back button to the previous desktop and the Forward button to the next desktop.
   - While enabled, the app consumes these two button presses globally; browsers and other applications will not also receive their normal Back/Forward action.

## Data and configuration

- Usage logs and date-specific reports are stored in `%USERPROFILE%\Documents\VirtualDesktopLogs` by default.
- Tracker configuration is saved as `tracker_config.json` in the same folder.
- Timely configuration is saved separately in that folder. Sensitive Timely values are encrypted for the current Windows user when the configuration is next saved.

The report dialog creates `usage_report_YYYY-MM-DD.txt` and `usage_report_YYYY-MM-DD.json`. Existing files for the same date are replaced when a report is generated again.

## Issue tracking and Timely

- [Issue tracking setup](ISSUE_TRACKING.md) explains regex patterns and issue URL templates.
- Timely is configured from the app with **Configure → Timely**. You can upload tracked entries through the menu once configuration is complete.

## Test and CI

```powershell
dotnet test .\virtual-desktop-tracker.sln --configuration Release
```

The GitHub Actions workflow restores packages, compiles the VirtualDesktop helper executables, runs tests, and builds the release solution on Windows.

## Troubleshooting

### The desktop display is visible only on one desktop

Ensure `VirtualDesktop11-24H2.exe` or `VirtualDesktop11.exe` is beside the application executable. Restart the app after adding or replacing the helper. The program also applies normal topmost window flags, but the helper is required to pin it across virtual desktops.

### The display shows an error or no desktop name

Verify that the helper executable matches your Windows version and can be run from the application directory. Then open **Extras → Tracking Status** for the last recorded error.

### The Back/Forward mouse buttons do not switch desktops

Open the right-click menu and enable **Configure → Use mouse Back/Forward buttons to switch desktops**. The option is intentionally off by default. It only works while VirtualDesktopDisplayer is running and there must be an adjacent desktop in the chosen direction.

### No report data is found

The report date must match the date of recorded usage. Check **Extras → Open Log Folder** and keep the app running while changing desktops so sessions are written.

### The app closes unexpectedly

Logs are flushed when the app closes normally. Restart the application, then check the current JSON log from **Extras → View Log JSON**.
