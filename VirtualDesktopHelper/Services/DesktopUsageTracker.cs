using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using VirtualDesktopHelper.Configuration;
using VirtualDesktopHelper.Interfaces;
using VirtualDesktopHelper.Models;

namespace VirtualDesktopHelper.Services
{
    /// <summary>
    /// Service for tracking and persisting virtual desktop usage data.
    /// </summary>
    public class DesktopUsageTracker : IDesktopUsageTracker
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true
        };

        private readonly TrackerConfiguration _config;
        private readonly IWindowsDesktopNameService _desktopNameService;
        private readonly IVirtualDesktopErrorHandler _errorHandler;
        private readonly string _logDirectory;
        private readonly string _logFilePath;
        private readonly List<DesktopUsageEntry> _currentSessionUsageLog;
        private readonly object _lockObject = new object();

        private string _currentDesktop = "";
        private DateTime _currentDesktopStartTime = DateTime.Now;

        // Constructor for clean service-based initialization
        public DesktopUsageTracker(TrackerConfiguration? config = null)
        {
            _config = config ?? TrackerConfiguration.Instance;
            // Use default implementations
            _desktopNameService = new WindowsDesktopNameService(
                new WindowsScreenStateDetector(),
                new VirtualDesktopErrorHandler(_config), 
                _config);
            _errorHandler = new VirtualDesktopErrorHandler(_config);
            _logDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                _config.LogDirectoryName
            );
            _logFilePath = Path.Combine(
                _logDirectory,
                string.Format(_config.LogFileNamePattern, DateTime.Now)
            );
            _currentSessionUsageLog = new List<DesktopUsageEntry>();

            EnsureLogDirectoryExists();
        }

        // Enhanced constructor with dependency injection
        public DesktopUsageTracker(
            IWindowsDesktopNameService desktopNameService,
            IVirtualDesktopErrorHandler errorHandler,
            TrackerConfiguration? config = null)
        {
            _desktopNameService = desktopNameService ?? throw new ArgumentNullException(nameof(desktopNameService));
            _errorHandler = errorHandler ?? throw new ArgumentNullException(nameof(errorHandler));
            _config = config ?? TrackerConfiguration.Instance;
            _logDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                _config.LogDirectoryName
            );
            _logFilePath = Path.Combine(
                _logDirectory,
                string.Format(_config.LogFileNamePattern, DateTime.Now)
            );
            _currentSessionUsageLog = new List<DesktopUsageEntry>();

            EnsureLogDirectoryExists();
        }

        public void TrackDesktopUsage(string desktopName)
        {
            if (string.IsNullOrWhiteSpace(desktopName))
                return;

            lock (_lockObject)
            {
                DateTime now = DateTime.Now;

                // If this is the same desktop, do nothing
                if (_currentDesktop == desktopName)
                    return;

                // Close the previous desktop session
                if (!string.IsNullOrEmpty(_currentDesktop))
                {
                    var lastEntry = _currentSessionUsageLog.LastOrDefault();
                    if (lastEntry != null && lastEntry.DesktopName == _currentDesktop && lastEntry.EndTime == null)
                    {
                        lastEntry.EndTime = now;
                    }
                }

                // Start tracking the new desktop
                _currentDesktop = desktopName;
                _currentDesktopStartTime = now;

                // Add new entry
                var newEntry = new DesktopUsageEntry
                {
                    DesktopName = desktopName,
                    StartTime = now,
                    EndTime = null // Still active
                };

                _currentSessionUsageLog.Add(newEntry);
                SaveCurrentSessionLog();
            }
        }

        public List<DesktopUsageEntry> GetCurrentSessionUsageLog()
        {
            lock (_lockObject)
            {
                return new List<DesktopUsageEntry>(_currentSessionUsageLog);
            }
        }

        public List<DesktopUsageEntry> GetAllUsageHistory()
        {
            var allEntries = new List<DesktopUsageEntry>();

            try
            {
                if (Directory.Exists(_logDirectory))
                {
                    var logFiles = Directory.GetFiles(_logDirectory, "VirtualDesktopUsage_*.json");

                    foreach (var file in logFiles)
                    {
                        try
                        {
                            var entries = LoadUsageLogFromFile(file);
                            if (entries != null)
                            {
                                allEntries.AddRange(entries);
                            }
                        }
                        catch
                        {
                            // Skip corrupted files
                        }
                    }
                }
            }
            catch
            {
                // Return empty list if we can't read files
            }

            return allEntries.OrderBy(e => e.StartTime).ToList();
        }

        public string GetCurrentLogFilePath()
        {
            return _logFilePath;
        }

        public string GetLogDirectory()
        {
            return _logDirectory;
        }

        public async Task GenerateUsageReportAsync()
        {
            try
            {
                var reportPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    _config.ReportFileName
                );

                var reportGenerator = new UsageReportGenerator(_config);
                var allEntries = GetAllUsageHistory();
                
                // Generate both text and JSON reports
                await reportGenerator.GenerateReportWithJsonAsync(allEntries, reportPath, currentDayOnly: true);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error generating usage report: {ex.Message}");
            }
        }

        private void EnsureLogDirectoryExists()
        {
            try
            {
                if (!Directory.Exists(_logDirectory))
                {
                    Directory.CreateDirectory(_logDirectory);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error creating log directory: {ex.Message}");
            }
        }

        private void SaveCurrentSessionLog()
        {
            try
            {
                SaveUsageLogToFile(_currentSessionUsageLog, _logFilePath);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving usage log: {ex.Message}");
            }
        }

        private List<DesktopUsageEntry>? LoadUsageLogFromFile(string filePath)
        {
            try
            {
                string json = File.ReadAllText(filePath);
                return JsonSerializer.Deserialize<List<DesktopUsageEntry>>(json);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Ends the current tracking session by setting the end time of any active entry.
        /// Should be called when the application is shutting down.
        /// </summary>
        public void StopTracking()
        {
            lock (_lockObject)
            {
                DateTime now = DateTime.Now;

                // Find the last active entry and close it
                var lastEntry = _currentSessionUsageLog.LastOrDefault();
                if (lastEntry != null && lastEntry.EndTime == null)
                {
                    lastEntry.EndTime = now;
                    SaveCurrentSessionLog();
                }
            }
        }

        /// <summary>
        /// Ensures all entries have proper end times before returning usage history.
        /// This method sets the end time to current time for any entries that are still active (EndTime = null).
        /// </summary>
        /// <returns>List of usage entries with all end times properly set.</returns>
        public List<DesktopUsageEntry> GetAllUsageHistoryWithClosedSessions()
        {
            var allEntries = GetAllUsageHistory();
            DateTime now = DateTime.Now;

            // Ensure all entries have end times set
            foreach (var entry in allEntries.Where(e => e.EndTime == null))
            {
                entry.EndTime = now;
            }

            return allEntries.OrderBy(e => e.StartTime).ToList();
        }

        /// <summary>
        /// Updates the desktop name for all entries with the specified old name from today only.
        /// This also renames the current desktop if it matches the old name.
        /// </summary>
        /// <param name="oldName">The current desktop name to update.</param>
        /// <param name="newName">The new desktop name to use.</param>
        /// <returns>True if the operation was successful (including desktop rename), false otherwise.</returns>
        public bool UpdateDesktopNameForTodaysEntries(string oldName, string newName)
        {
            if (string.IsNullOrWhiteSpace(oldName) || string.IsNullOrWhiteSpace(newName))
                return false;

            lock (_lockObject)
            {
                try
                {
                    bool desktopRenameSuccess = true;
                    DateTime today = DateTime.Today;

                    // First, rename the current desktop if it matches the old name
                    if (_currentDesktop == oldName)
                    {
                        desktopRenameSuccess = _desktopNameService.RenameCurrentDesktop(newName);
                        if (desktopRenameSuccess)
                        {
                            _currentDesktop = newName;
                        }
                    }

                    // Update current session entries from today
                    bool currentSessionUpdated = false;
                    foreach (var entry in _currentSessionUsageLog.Where(e => 
                        e.DesktopName == oldName && e.StartTime.Date == today))
                    {
                        entry.DesktopName = newName;
                        currentSessionUpdated = true;
                    }

                    // Update historical log files from today
                    bool historicalUpdated = UpdateTodaysLogFiles(oldName, newName, today);

                    // Save current session if we made changes
                    if (currentSessionUpdated)
                    {
                        SaveCurrentSessionLog();
                    }

                    return desktopRenameSuccess && (currentSessionUpdated || historicalUpdated);
                }
                catch (Exception ex)
                {
                    _errorHandler.LogError(ex, "UpdateDesktopNameForTodaysEntries", new { OldName = oldName, NewName = newName });
                    return false;
                }
            }
        }

        /// <summary>
        /// Updates today's log files to replace the old desktop name with the new one.
        /// </summary>
        /// <param name="oldName">The old desktop name to replace.</param>
        /// <param name="newName">The new desktop name to use.</param>
        /// <param name="today">Today's date for filtering.</param>
        /// <returns>True if any files were updated, false otherwise.</returns>
        private bool UpdateTodaysLogFiles(string oldName, string newName, DateTime today)
        {
            bool anyFileUpdated = false;

            try
            {
                if (!Directory.Exists(_logDirectory))
                    return false;

                var logFiles = Directory.GetFiles(_logDirectory, "VirtualDesktopUsage_*.json");
                var todayPattern = today.ToString("yyyy-MM-dd");

                foreach (var file in logFiles)
                {
                    var fileName = Path.GetFileName(file);
                    if (!fileName.Contains(todayPattern))
                        continue; // Skip files that are not from today

                    try
                    {
                        var entries = LoadUsageLogFromFile(file);
                        if (entries == null) continue;

                        bool fileModified = false;
                        foreach (var entry in entries.Where(e => 
                            e.DesktopName == oldName && e.StartTime.Date == today))
                        {
                            entry.DesktopName = newName;
                            fileModified = true;
                        }

                        if (fileModified)
                        {
                            SaveUsageLogToFile(entries, file);
                            anyFileUpdated = true;
                        }
                    }
                    catch (Exception ex)
                    {
                        _errorHandler.LogError(ex, "UpdateTodaysLogFiles", new { FileName = file });
                        // Continue with other files even if one fails
                    }
                }
            }
            catch (Exception ex)
            {
                _errorHandler.LogError(ex, "UpdateTodaysLogFiles", new { OldName = oldName, NewName = newName });
            }

            return anyFileUpdated;
        }

        /// <summary>
        /// Saves usage log entries to the specified file.
        /// </summary>
        /// <param name="entries">The entries to save.</param>
        /// <param name="filePath">The file path to save to.</param>
        private void SaveUsageLogToFile(List<DesktopUsageEntry> entries, string filePath)
        {
            string? directory = Path.GetDirectoryName(filePath);
            if (string.IsNullOrEmpty(directory))
                throw new InvalidOperationException($"Log file path has no directory: {filePath}");

            Directory.CreateDirectory(directory);

            string temporaryFilePath = Path.Combine(directory, $".{Path.GetFileName(filePath)}.{Guid.NewGuid():N}.tmp");
            try
            {
                string json = JsonSerializer.Serialize(entries, JsonOptions);
                File.WriteAllText(temporaryFilePath, json);
                File.Move(temporaryFilePath, filePath, overwrite: true);
            }
            finally
            {
                if (File.Exists(temporaryFilePath))
                    File.Delete(temporaryFilePath);
            }
        }
    }
}
