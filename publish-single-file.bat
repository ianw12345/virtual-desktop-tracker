@echo off
set "PUBLISH_DIR=%~dp0publish"
echo Publishing the self-contained single-file application...
dotnet publish "%~dp0VirtualDesktopDisplayer\VirtualDesktopDisplayer.csproj" --configuration Release --output "%PUBLISH_DIR%"
if errorlevel 1 exit /b %errorlevel%
echo.
echo Release created: %PUBLISH_DIR%\VirtualDesktopDisplayer.exe
