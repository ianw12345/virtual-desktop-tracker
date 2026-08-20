# Virtual Desktop Tracker

Virtual Desktop Tracker is a single Windows desktop application for showing the active virtual desktop, recording how long it is used, and analysing that history. The executable is **VirtualDesktopDisplayer**; there is no separate command-line tracker.

## Features

- Always-on-top desktop-name display, available across virtual desktops
- Automatic usage tracking, including a `Screen Off` state when Windows is locked or the display is unavailable
- Rename, create, and jump to virtual desktops from the right-click menu
- Date-selectable working-hours estimation
- Date-selectable text and JSON reports with configurable activity consolidation
- Timeline, project detection, issue tracking, and Timely or Clockify upload integration
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

- Windows 11 24H2 or later (build 26100+)
- .NET SDK 9.0 or later
- The helper executables from [MScholtes/VirtualDesktop](https://github.com/MScholtes/VirtualDesktop)

The application bundles the `VirtualDesktop11-24H2.exe` helper. At runtime it is extracted to a private per-user cache because Windows can execute native helpers only from a file. The published release remains a single EXE.

### Build the VirtualDesktop helper

From the repository root:

```powershell
git clone https://github.com/MScholtes/VirtualDesktop.git VirtualDesktop
& 'C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe' .\VirtualDesktop\VirtualDesktop11-24H2.cs /out:.\VirtualDesktop\VirtualDesktop11-24H2.exe /win32icon:.\VirtualDesktop\VirtualDesktop.ico
```

Only the 24H2 helper is supported. GitHub Actions builds and bundles it automatically.

## Build and start

```powershell
dotnet restore .\virtual-desktop-tracker.sln
dotnet publish .\VirtualDesktopDisplayer\VirtualDesktopDisplayer.csproj --configuration Release --output .\publish
.\publish\VirtualDesktopDisplayer.exe
```

The `publish` directory contains exactly one self-contained executable. For development, run `.\run-displayer.bat`; for a release, use `.\publish-single-file.bat` and then `.\Run-VirtualDesktopDisplayer.bat`.

## Using the app

1. Start `VirtualDesktopDisplayer.exe`. The active desktop name appears at the bottom-right corner.
2. Right-click the display to open the menu. Double-click it to exit.
3. Use **Rename** options to name a desktop after the task or issue you are working on.
4. Open **Extras** for analysis and diagnostics:
   - **Working Hours...** calculates total time, remaining time, lunch break, and an estimated finish time for a selected date.
   - **Generate Report** creates text and JSON reports for a selected date. Its dialog exposes the former CLI options: activity consolidation, minimum duration, and maximum custom duration.
   - **Tracking Status** shows the current desktop, most recent poll, intervals, log location, and last tracking error.
   - **View Log JSON** and **Open Log Folder** provide direct access to persisted data.
5. Use **Configure** to set up Timely, Clockify, project mappings, issue tracking, and mouse navigation.
   - Use **Extras → Start tracking** to begin an explicit work session. Use **Stop tracking & upload to Clockify** to end it and upload its recorded desktop intervals.
   - Enable **Use mouse Back/Forward buttons to switch desktops** to map the dedicated Back button to the previous desktop and the Forward button to the next desktop. The order is cyclic: `1 → 2 → 3 → 1` and `1 → 3` when navigating back.
   - While enabled, the app consumes these two button presses globally; browsers and other applications will not also receive their normal Back/Forward action.

## Data and configuration

- Usage logs and date-specific reports are stored in `%USERPROFILE%\Documents\VirtualDesktopLogs` by default.
- Tracker configuration is saved as `tracker_config.json` in the same folder.
- Timely and Clockify configuration are saved separately in that folder. Sensitive values, including the Clockify API key, are encrypted for the current Windows user when the configuration is saved.

The report dialog creates `usage_report_YYYY-MM-DD.txt` and `usage_report_YYYY-MM-DD.json`. Existing files for the same date are replaced when a report is generated again.

## Issue tracking and time tracking

- [Issue tracking setup](ISSUE_TRACKING.md) explains regex patterns and issue URL templates.
- Timely is configured from the app with **Configure → Timely**. It retains the existing cookie-based integration.
- Clockify is configured from the app with **Configure → Clockify**. Enter an API key, choose the workspace and default project, then use **Desktop assignments…** to map named virtual desktops to different Clockify projects. An exact desktop-name assignment overrides the default project. Use **Upload to Clockify** to create one regular time entry per consolidated desktop interval; the entry description is `Virtual desktop: <desktop name>`.
- Desktop usage is recorded only between **Start tracking** and **Stop tracking & upload to Clockify**. Stopping first saves the current desktop interval, then uploads all entries from that session to Clockify.
- Clockify uses its public v1 API and an `X-Api-Key` header. The key is not written to the usage log or error messages. See the [Clockify API documentation](https://docs.clockify.me/).

## Test and CI

```powershell
dotnet test .\virtual-desktop-tracker.sln --configuration Release
```

The GitHub Actions workflow restores packages, compiles the VirtualDesktop helper executables, runs tests, and builds the release solution on Windows.

## Troubleshooting

### The desktop display is visible only on one desktop

This release supports only Windows 11 24H2 (build 26100+) and bundles its required helper. If the display is not pinned after an update, restart the app so it can refresh its private helper cache.

### The display shows an error or no desktop name

Verify that the helper executable matches your Windows version and can be run from the application directory. Then open **Extras → Tracking Status** for the last recorded error.

### The app reports an unsupported Windows version

The application requires Windows 11 24H2, build 26100 or later. This is checked before the main window opens because the bundled desktop helper targets that Windows virtual-desktop API version.

### The Back/Forward mouse buttons do not switch desktops

Open the right-click menu and enable **Configure → Use mouse Back/Forward buttons to switch desktops**. The option is intentionally off by default and works only while VirtualDesktopDisplayer is running. Navigation wraps around the available desktops.

### No report data is found

The report date must match the date of recorded usage. Check **Extras → Open Log Folder** and keep the app running while changing desktops so sessions are written.

### The app closes unexpectedly

Logs are flushed when the app closes normally. Restart the application, then check the current JSON log from **Extras → View Log JSON**.
