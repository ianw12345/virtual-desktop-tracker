# VirtualDesktopDisplayer

`VirtualDesktopDisplayer.exe` is the single graphical application in this repository. It displays the active Windows virtual desktop, records usage, and provides all analysis and configuration functions from its right-click menu.

## Main controls

- **Right-click the display** to rename desktops, open reports and logs, view the timeline, configure integrations, or exit.
- **Double-click the display** to exit.
- **Configure → Use mouse Back/Forward buttons to switch desktops** optionally maps the dedicated mouse Back button to the previous desktop and the Forward button to the next desktop.

The mouse option is disabled by default and is stored in the tracker configuration. When enabled, it consumes those buttons globally while the app is running, so foreground programs do not also navigate back or forward.

## Build and run

From the repository root:

```powershell
dotnet build .\virtual-desktop-tracker.sln --configuration Release
.\VirtualDesktopDisplayer\bin\Release\net9.0-windows\VirtualDesktopDisplayer.exe
```

The application uses `VirtualDesktop11-24H2.exe` or `VirtualDesktop11.exe` to retrieve, pin, rename, and switch virtual desktops. The `VirtualDesktopHelper` project copies the helpers from the repository's `VirtualDesktop` directory into the application output during the build.

For full setup, usage, reports, integrations, and troubleshooting, see the [root README](../README.md).
