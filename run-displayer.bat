@echo off
echo Starting Virtual Desktop Tracker in development mode...
echo This app will:
echo - Display current virtual desktop name in the corner
echo - Automatically track your desktop usage
echo - Save logs for analysis
echo - Right-click for reports, working hours, and tracking status
echo - Double-click to exit
echo.
cd /d "%~dp0VirtualDesktopDisplayer"
dotnet run
pause
