using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using VirtualDesktopHelper.Configuration;
using VirtualDesktopHelper.Interfaces;

namespace VirtualDesktopHelper.Services
{
    /// <summary>
    /// Windows virtual desktop name service that communicates with the bundled 24H2 helper subprocess.
    /// Provides reliable desktop name operations with error handling and retry mechanisms.
    /// </summary>
    public class WindowsDesktopNameService : IWindowsDesktopNameService
    {
        private readonly TrackerConfiguration _config;
        private readonly IScreenStateDetector _screenStateDetector;
        private readonly IVirtualDesktopErrorHandler _errorHandler;
        private string? _cachedExecutablePath;

        public WindowsDesktopNameService(
            IScreenStateDetector screenStateDetector,
            IVirtualDesktopErrorHandler errorHandler,
            TrackerConfiguration? config = null)
        {
            _screenStateDetector = screenStateDetector ?? throw new ArgumentNullException(nameof(screenStateDetector));
            _errorHandler = errorHandler ?? throw new ArgumentNullException(nameof(errorHandler));
            _config = config ?? TrackerConfiguration.Instance;
        }

        public string GetCurrentDesktopName()
        {
            return _errorHandler.ExecuteWithRetry(() =>
            {
                // First check if screen is locked or off
                if (_screenStateDetector.IsScreenLockedOrOff())
                {
                    return "Screen Off";
                }

                return GetCurrentDesktopNameFromSubprocess();
            }, "GetCurrentDesktopName", _config.SubprocessRetryCount, _config.SubprocessRetryDelay);
        }

        public bool RenameCurrentDesktop(string newName)
        {
            if (string.IsNullOrWhiteSpace(newName))
            {
                _errorHandler.LogWarning("Empty or null desktop name provided", "RenameCurrentDesktop");
                return false;
            }

            return _errorHandler.ExecuteWithRetry(() =>
            {
                string executablePath = GetVirtualDesktopExecutablePath();
                return ExecuteRenameCommand(executablePath, newName);
            }, "RenameCurrentDesktop", _config.SubprocessRetryCount, _config.SubprocessRetryDelay);
        }

        public List<string> GetAllDesktopNames()
        {
            return _errorHandler.ExecuteWithRetry(() =>
            {
                string executablePath = GetVirtualDesktopExecutablePath();
                string output = ExecuteVirtualDesktopCommand(executablePath, "/LIST");
                return ParseAllDesktopNamesFromOutput(output);
            }, "GetAllDesktopNames", _config.SubprocessRetryCount, _config.SubprocessRetryDelay);
        }

        public bool SwitchToDesktop(string desktopName)
        {
            if (string.IsNullOrWhiteSpace(desktopName))
            {
                _errorHandler.LogWarning("Empty or null desktop name provided", "SwitchToDesktop");
                return false;
            }

            return _errorHandler.ExecuteWithRetry(() =>
            {
                string executablePath = GetVirtualDesktopExecutablePath();
                return ExecuteSwitchCommand(executablePath, desktopName);
            }, "SwitchToDesktop", _config.SubprocessRetryCount, _config.SubprocessRetryDelay);
        }

        public bool CreateNewDesktop(bool switchToNew = true)
        {
            return _errorHandler.ExecuteWithRetry(() =>
            {
                string executablePath = GetVirtualDesktopExecutablePath();
                return ExecuteCreateNewDesktopCommand(executablePath, switchToNew);
            }, "CreateNewDesktop", _config.SubprocessRetryCount, _config.SubprocessRetryDelay);
        }

        private string GetCurrentDesktopNameFromSubprocess()
        {
            string executablePath = GetVirtualDesktopExecutablePath();

            for (int attempt = 0; attempt < _config.SubprocessRetryCount; attempt++)
            {
                try
                {
                    string output = ExecuteVirtualDesktopCommand(executablePath, "/LIST");
                    string desktopName = ParseDesktopNameFromOutput(output);

                    if (!string.IsNullOrEmpty(desktopName))
                        return desktopName;
                }
                catch (Exception ex) when (attempt < _config.SubprocessRetryCount - 1)
                {
                    System.Diagnostics.Debug.WriteLine($"Attempt {attempt + 1} failed: {ex.Message}");
                    System.Threading.Thread.Sleep(_config.SubprocessRetryDelay);
                }
            }

            return "Unknown Desktop";
        }

        private string GetVirtualDesktopExecutablePath()
        {
            if (_cachedExecutablePath != null && File.Exists(_cachedExecutablePath))
                return _cachedExecutablePath;

            string executablePath = VirtualDesktopExecutableProvider.GetPath();

            _cachedExecutablePath = executablePath;
            return executablePath;
        }

        private string ExecuteVirtualDesktopCommand(string executablePath, string arguments)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = executablePath,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using (Process? process = Process.Start(startInfo))
            {
                if (process == null)
                    throw new InvalidOperationException("Failed to start VirtualDesktop process");

                bool finished = process.WaitForExit((int)_config.SubprocessTimeout.TotalMilliseconds);
                if (!finished)
                {
                    process.Kill();
                    throw new TimeoutException($"VirtualDesktop process timed out after {_config.SubprocessTimeout}");
                }

                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();

                if (!string.IsNullOrEmpty(error))
                    throw new InvalidOperationException($"VirtualDesktop process error: {error}");

                return output;
            }
        }

        private bool ExecuteRenameCommand(string executablePath, string newName)
        {
            try
            {
                // First get the current desktop number, then set its name
                // The /GetCurrentDesktop command puts the desktop number in the pipeline
                // Then /Name: uses that number to set the name
                string output = ExecuteVirtualDesktopCommand(executablePath, $"/GetCurrentDesktop /Name:\"{newName}\"");
                return true; // If no exception was thrown, assume success
            }
            catch (Exception ex)
            {
                _errorHandler.LogError(ex, "ExecuteRenameCommand", new { DesktopName = newName });
                return false;
            }
        }

        private static string ParseDesktopNameFromOutput(string output)
        {
            if (string.IsNullOrEmpty(output))
                return "";

            string[] lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            foreach (string line in lines)
            {
                string trimmedLine = line.Trim();
                if (trimmedLine.EndsWith("(visible)"))
                {
                    // Remove "(visible)" suffix and return the desktop name
                    return trimmedLine.Substring(0, trimmedLine.Length - "(visible)".Length).Trim();
                }
            }

            return "";
        }

        private static List<string> ParseAllDesktopNamesFromOutput(string output)
        {
            var desktopNames = new List<string>();
            
            if (string.IsNullOrEmpty(output))
                return desktopNames;

            string[] lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            foreach (string line in lines)
            {
                string trimmedLine = line.Trim();
                
                // Skip header lines and empty lines
                if (string.IsNullOrEmpty(trimmedLine) || 
                    trimmedLine.StartsWith("Virtual desktops:") ||
                    trimmedLine.StartsWith("-----------------") ||
                    trimmedLine.StartsWith("Count of desktops:"))
                {
                    continue;
                }

                // Extract desktop name, removing "(visible)" suffix if present
                string desktopName = trimmedLine.EndsWith("(visible)") 
                    ? trimmedLine.Substring(0, trimmedLine.Length - "(visible)".Length).Trim()
                    : trimmedLine;

                if (!string.IsNullOrEmpty(desktopName))
                {
                    desktopNames.Add(desktopName);
                }
            }

            return desktopNames;
        }

        private bool ExecuteSwitchCommand(string executablePath, string desktopName)
        {
            try
            {
                // Use the /Switch command with the desktop name
                string output = ExecuteVirtualDesktopCommand(executablePath, $"/Switch:\"{desktopName}\"");
                return true; // If no exception was thrown, assume success
            }
            catch (Exception ex)
            {
                _errorHandler.LogError(ex, "ExecuteSwitchCommand", new { DesktopName = desktopName });
                return false;
            }
        }

        private bool ExecuteCreateNewDesktopCommand(string executablePath, bool switchToNew)
        {
            try
            {
                // Use /New to create a new desktop, and optionally /Switch to switch to it
                string command = switchToNew ? "/New /Switch" : "/New";
                string output = ExecuteVirtualDesktopCommand(executablePath, command);
                return true; // If no exception was thrown, assume success
            }
            catch (Exception ex)
            {
                _errorHandler.LogError(ex, "ExecuteCreateNewDesktopCommand", new { SwitchToNew = switchToNew });
                return false;
            }
        }
    }
}
