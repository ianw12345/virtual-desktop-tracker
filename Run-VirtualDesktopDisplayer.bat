@echo off
echo Starting Virtual Desktop Tracker...
echo.
echo Features:
echo - Shows current virtual desktop name in corner
echo - Automatically tracks and logs your desktop usage
echo - Right-click for reports, working hours, and tracking status
echo - Double-click to exit
echo.
set "APP=%~dp0VirtualDesktopDisplayer\bin\Release\net9.0-windows\VirtualDesktopDisplayer.exe"
if not exist "%APP%" (
    echo Release executable not found. Build it with: dotnet build virtual-desktop-tracker.sln --configuration Release
    pause
    exit /b 1
)
start "" "%APP%"
