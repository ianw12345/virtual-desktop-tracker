@echo off
REM Virtual Desktop Tracker startup script
REM This script starts the release GUI from the repository location.

set "APP=%~dp0VirtualDesktopDisplayer\bin\Release\net9.0-windows\VirtualDesktopDisplayer.exe"
if exist "%APP%" start "" "%APP%"
