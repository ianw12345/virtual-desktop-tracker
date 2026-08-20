using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using VirtualDesktopHelper.Configuration;
using VirtualDesktopHelper.Interfaces;
using VirtualDesktopHelper.Models;

namespace VirtualDesktopHelper.Services
{
    /// <summary>
    /// Service for uploading time entries directly to Timely via HTTP API
    /// </summary>
    public class TimelyApiService : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly TimelyConfiguration _timelyConfig;
        private readonly IUsageConsolidationService _consolidationService;
        private readonly ProjectDetectionService _projectDetectionService;
        private bool _disposed = false;

        public TimelyApiService(IUsageConsolidationService? consolidationService = null)
        {
            _timelyConfig = TimelyConfiguration.Instance;
            _httpClient = TimelyHttpClientFactory.Create(_timelyConfig, includeUploadHeaders: true);
            _consolidationService = consolidationService ?? new UsageConsolidationService();
            _projectDetectionService = new ProjectDetectionService();
        }

        /// <summary>
        /// Uploads desktop usage data directly to Timely via API.
        /// </summary>
        /// <param name="allEntries">All desktop usage entries.</param>
        /// <param name="currentDayOnly">If true, only uploads entries from the current day.</param>
        /// <returns>Result of the upload operation.</returns>
        public async Task<TimelyUploadResult> UploadToTimelyAsync(List<DesktopUsageEntry> allEntries, bool currentDayOnly = true)
        {
            return await UploadToTimelyAsync(allEntries, currentDayOnly, null);
        }

        /// <summary>
        /// Uploads desktop usage data directly to Timely via API with optional time filtering.
        /// </summary>
        /// <param name="allEntries">All desktop usage entries.</param>
        /// <param name="currentDayOnly">If true, only uploads entries from the current day.</param>
        /// <param name="fromTime">If specified, only uploads entries ending after this time.</param>
        /// <returns>Result of the upload operation.</returns>
        public async Task<TimelyUploadResult> UploadToTimelyAsync(List<DesktopUsageEntry> allEntries, bool currentDayOnly = true, DateTime? fromTime = null)
        {
            return await UploadToTimelyAsync(allEntries, currentDayOnly, fromTime, null);
        }

        /// <summary>
        /// Uploads desktop usage data directly to Timely via API for a specific date.
        /// </summary>
        /// <param name="allEntries">All desktop usage entries.</param>
        /// <param name="targetDate">The specific date to upload entries for. If null, uses current day filtering.</param>
        /// <param name="fromTime">If specified, only uploads entries ending after this time.</param>
        /// <returns>Result of the upload operation.</returns>
        public async Task<TimelyUploadResult> UploadToTimelyAsync(List<DesktopUsageEntry> allEntries, DateTime? targetDate, DateTime? fromTime = null)
        {
            return await UploadToTimelyAsync(allEntries, targetDate == null, fromTime, targetDate);
        }

        /// <summary>
        /// Internal method that handles all upload scenarios with optional date and time filtering.
        /// </summary>
        /// <param name="allEntries">All desktop usage entries.</param>
        /// <param name="currentDayOnly">If true, only uploads entries from the current day.</param>
        /// <param name="fromTime">If specified, only uploads entries ending after this time.</param>
        /// <param name="targetDate">If specified, uploads entries for this specific date instead of current day.</param>
        /// <returns>Result of the upload operation.</returns>
        private async Task<TimelyUploadResult> UploadToTimelyAsync(List<DesktopUsageEntry> allEntries, bool currentDayOnly, DateTime? fromTime, DateTime? targetDate)
        {
            var result = new TimelyUploadResult();
            
            try
            {
                // Check configuration
                if (!_timelyConfig.IsConfigured())
                {
                    var error = "Timely configuration is incomplete. Please ensure CSRF token, cookies, and IDs are set.";
                    result.Errors.Add(error);
                    LogError(error);
                    return result;
                }

                // Ensure all entries have proper end times
                var entriesWithEndTime = DesktopUsageUtilities.EnsureEndTimesAreSet(allEntries);

                // Filter entries by date
                List<DesktopUsageEntry> filteredEntries;
                if (targetDate.HasValue)
                {
                    // Filter for specific date
                    filteredEntries = DesktopUsageUtilities.FilterEntriesByDate(entriesWithEndTime, targetDate.Value);
                }
                else if (currentDayOnly)
                {
                    // Filter for current day
                    filteredEntries = DesktopUsageUtilities.FilterCurrentDayEntries(entriesWithEndTime);
                }
                else
                {
                    // Use all entries
                    filteredEntries = entriesWithEndTime;
                }

                // Filter entries from specific time if requested
                if (fromTime.HasValue)
                {
                    filteredEntries = DesktopUsageUtilities.FilterEntriesFromTime(filteredEntries, fromTime.Value);
                }

                if (!filteredEntries.Any())
                {
                    var dateDescription = targetDate?.ToString("yyyy-MM-dd") ?? (currentDayOnly ? "today" : "the specified range");
                    var error = $"No usage data available for {dateDescription}.";
                    result.Errors.Add(error);
                    LogError(error);
                    return result;
                }

                // Get the actual target date for upload (use provided date or today)
                var uploadDate = targetDate ?? DateTime.Today;

                // Consolidate entries first
                var consolidatedEntries = _consolidationService.ConsolidateUsageEntries(filteredEntries);
                
                // Filter out entries with zero duration after ceiling to minutes
                var validEntries = DesktopUsageUtilities.FilterZeroDurationEntries(consolidatedEntries);
                
                if (!validEntries.Any())
                {
                    var error = "No entries to upload after consolidation and filtering zero-duration entries";
                    result.Errors.Add(error);
                    LogError(error);
                    return result;
                }

                LogInfo($"Starting upload of {validEntries.Count} consolidated activities for {targetDate:yyyy-MM-dd}");

                // Group activities by desktop name (like TimelyJavaScript generator does)
                var desktopGroups = GroupActivitiesByDesktopName(validEntries);
                
                LogInfo($"Grouped into {desktopGroups.Count} desktop groups for upload");

                // Upload each desktop group as a single entry
                foreach (var kvp in desktopGroups)
                {
                    string desktopName = kvp.Key;
                    var activities = kvp.Value;
                    
                    try
                    {
                        await UploadDesktopGroup(desktopName, activities, uploadDate);
                        result.SuccessCount++;
                        LogInfo($"Successfully uploaded desktop group: {desktopName}");
                    }
                    catch (Exception ex)
                    {
                        var error = $"Failed to upload desktop group: {desktopName}";
                        var detailedError = $"{error} - {ex.Message}";
                        result.Errors.Add(error);
                        LogError(detailedError, ex);
                    }
                }

                LogInfo($"Upload completed. Success: {result.SuccessCount}, Failed: {result.FailureCount}");
            }
            catch (Exception ex)
            {
                var error = $"General upload error: {ex.Message}";
                result.Errors.Add(error);
                LogError(error, ex);
            }
            
            return result;
        }

        private async Task UploadSingleActivity(DesktopUsageEntry activity, DateTime targetDate)
        {
            // Detect project for this activity's desktop
            var project = _projectDetectionService.DetectProjectForEntry(activity);
            
            // Calculate ceiled duration in minutes
            var totalMinutes = DesktopUsageUtilities.CalculateCeiledDurationInMinutes(activity);
            
            if (totalMinutes <= 0)
            {
                throw new InvalidOperationException("No valid time duration found after ceiling to minutes");
            }

            // Create timestamp for this activity using ceiled times
            var ceiledTimes = DesktopUsageUtilities.GetCeiledTimes(activity);
            var timestamp = new
            {
                from = ceiledTimes.CeiledStartTime.ToString("yyyy-MM-ddTHH:mm:ss.fff") + _timelyConfig.TimezoneOffset,
                to = ceiledTimes.CeiledEndTime.ToString("yyyy-MM-ddTHH:mm:ss.fff") + _timelyConfig.TimezoneOffset,
                entry_ids = new int[0]
            };

            // Use the activity's desktop name as the note
            var note = activity.DesktopName;

            // Create the request payload matching the example format exactly
            var eventData = new
            {
                @event = new
                {
                    day = targetDate.ToString("yyyy-MM-dd"),
                    note = note,
                    timer_state = "default",
                    timer_started_on = 0,
                    timer_stopped_on = 0,
                    project_id = project.Id,
                    forecast_id = (int?)null,
                    label_ids = project.LabelIds?.ToArray() ?? new int[0],
                    user_ids = new int[0],
                    entry_ids = new int[0],
                    from = timestamp.from,
                    to = timestamp.to,
                    timestamps = new[] { timestamp },
                    hours = totalMinutes / 60,
                    minutes = totalMinutes % 60,
                    seconds = 0,
                    estimated_hours = 0,
                    estimated_minutes = 0,
                    sequence = 1,
                    billable = false,
                    context = new
                    {
                        interaction = "Timestamp Selection",
                        view_context = "Calendar",
                        memory_experience = "Old",
                        memory_view = "Timeline",
                        calendar_view = "Day",
                        has_timer = false
                    },
                    state_id = (int?)null,
                    billed = false,
                    locked = false,
                    locked_reason = (string?)null,
                    external_links = new object[0],
                    user_id = _timelyConfig.UserId
                }
            };

            var json = JsonSerializer.Serialize(eventData, new JsonSerializerOptions
            {
                WriteIndented = false // Use compact JSON for API calls
            });

            var content = new StringContent(json, Encoding.UTF8, "application/json");
            
            var url = $"{_timelyConfig.ApiBaseUrl}/{_timelyConfig.WorkspaceId}/hours";
            var referer = $"{_timelyConfig.ApiBaseUrl}/{_timelyConfig.WorkspaceId}/calendar/day?date={targetDate:yyyy-MM-dd}&multiUserMode=false";
            
            LogInfo($"Uploading to Timely: {note} ({totalMinutes} minutes) to project {project.Name} (ID: {project.Id})");
            LogInfo($"Request URL: {url}");
            LogInfo($"Request payload: {json}");
            
            var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Add("referer", referer);
            request.Content = content;
            
            var response = await _httpClient.SendAsync(request);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                LogError($"HTTP {response.StatusCode} error: {errorContent}");
                throw new HttpRequestException($"HTTP {response.StatusCode}: {errorContent}");
            }
            
            var responseContent = await response.Content.ReadAsStringAsync();
            LogInfo($"Upload response: {responseContent}");
        }

        /// <summary>
        /// Groups activities by desktop name, similar to how TimelyJavaScript generator works.
        /// Each desktop name will have one submission with multiple timestamps if needed.
        /// </summary>
        private Dictionary<string, List<DesktopUsageEntry>> GroupActivitiesByDesktopName(List<DesktopUsageEntry> activities)
        {
            var groups = new Dictionary<string, List<DesktopUsageEntry>>();
            
            foreach (var activity in activities.OrderBy(a => a.StartTime))
            {
                if (!groups.ContainsKey(activity.DesktopName))
                {
                    groups[activity.DesktopName] = new List<DesktopUsageEntry>();
                }
                groups[activity.DesktopName].Add(activity);
            }
            
            return groups;
        }

        /// <summary>
        /// Uploads a group of activities for the same desktop name as a single Timely entry.
        /// This matches the behavior of the TimelyJavaScript generator.
        /// </summary>
        private async Task UploadDesktopGroup(string desktopName, List<DesktopUsageEntry> activities, DateTime targetDate)
        {
            if (!activities.Any())
                throw new InvalidOperationException("No activities to upload");

            // Use the first activity to determine the project (all activities for same desktop should map to same project)
            var firstActivity = activities.First();
            var project = _projectDetectionService.DetectProjectForEntry(firstActivity);
            
            // Calculate total duration using ceiled minutes for each activity
            var totalMinutes = activities.Sum(a => DesktopUsageUtilities.CalculateCeiledDurationInMinutes(a));
            
            if (totalMinutes <= 0)
            {
                throw new InvalidOperationException("No valid time duration found for desktop group after ceiling to minutes");
            }

            // Create timestamps for each activity period using ceiled times
            var timestamps = activities.Select(activity => 
            {
                var ceiledTimes = DesktopUsageUtilities.GetCeiledTimes(activity);
                return new
                {
                    from = ceiledTimes.CeiledStartTime.ToString("yyyy-MM-ddTHH:mm:ss.fff") + _timelyConfig.TimezoneOffset,
                    to = ceiledTimes.CeiledEndTime.ToString("yyyy-MM-ddTHH:mm:ss.fff") + _timelyConfig.TimezoneOffset,
                    entry_ids = new int[0]
                };
            }).ToArray();

            // Use the desktop name as the note
            var note = desktopName;

            // Create the request payload - use first and last timestamp for main from/to
            var firstTimestamp = timestamps.First();
            var lastTimestamp = timestamps.Last();

            var eventData = new
            {
                @event = new
                {
                    day = targetDate.ToString("yyyy-MM-dd"),
                    note = note,
                    timer_state = "default",
                    timer_started_on = 0,
                    timer_stopped_on = 0,
                    project_id = project.Id,
                    forecast_id = (int?)null,
                    label_ids = project.LabelIds?.ToArray() ?? new int[0],
                    user_ids = new int[0],
                    entry_ids = new int[0],
                    from = firstTimestamp.from,
                    to = lastTimestamp.to,
                    timestamps = timestamps,
                    hours = totalMinutes / 60,
                    minutes = totalMinutes % 60,
                    seconds = 0,
                    estimated_hours = 0,
                    estimated_minutes = 0,
                    sequence = 1,
                    billable = false,
                    context = new
                    {
                        interaction = "Timestamp Selection",
                        view_context = "Calendar",
                        memory_experience = "Old",
                        memory_view = "Timeline",
                        calendar_view = "Day",
                        has_timer = false
                    },
                    state_id = (int?)null,
                    billed = false,
                    locked = false,
                    locked_reason = (string?)null,
                    external_links = new object[0],
                    user_id = _timelyConfig.UserId
                }
            };

            var json = JsonSerializer.Serialize(eventData, new JsonSerializerOptions
            {
                WriteIndented = false // Use compact JSON for API calls
            });

            var content = new StringContent(json, Encoding.UTF8, "application/json");
            
            var url = $"{_timelyConfig.ApiBaseUrl}/{_timelyConfig.WorkspaceId}/hours";
            var referer = $"{_timelyConfig.ApiBaseUrl}/{_timelyConfig.WorkspaceId}/calendar/day?date={targetDate:yyyy-MM-dd}&multiUserMode=false";
            
            LogInfo($"Uploading desktop group to Timely: {note} ({activities.Count} periods, {totalMinutes} total minutes) to project {project.Name} (ID: {project.Id})");
            LogInfo($"Request URL: {url}");
            LogInfo($"Request payload: {json}");
            
            var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Add("referer", referer);
            request.Content = content;
            
            var response = await _httpClient.SendAsync(request);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                LogError($"HTTP {response.StatusCode} error: {errorContent}");
                throw new HttpRequestException($"HTTP {response.StatusCode}: {errorContent}");
            }
            
            var responseContent = await response.Content.ReadAsStringAsync();
            LogInfo($"Upload response: {responseContent}");
        }

        private void LogError(string message, Exception? exception = null)
        {
            try
            {
                var logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ERROR: {message}";
                if (exception != null)
                {
                    logEntry += $"\nException: {exception}";
                }
                logEntry += "\n";
                
                var logPath = GetErrorLogPath();
                File.AppendAllText(logPath, logEntry);
                
                // Also write to debug output
                System.Diagnostics.Debug.WriteLine($"TimelyApiService ERROR: {message}");
                if (exception != null)
                {
                    System.Diagnostics.Debug.WriteLine($"Exception: {exception}");
                }
            }
            catch
            {
                // Don't throw errors from logging
                System.Diagnostics.Debug.WriteLine($"Failed to log error: {message}");
            }
        }

        private void LogInfo(string message)
        {
            try
            {
                var logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] INFO: {message}\n";
                var logPath = GetErrorLogPath(); // Using same log file for simplicity
                File.AppendAllText(logPath, logEntry);
                
                // Also write to debug output
                System.Diagnostics.Debug.WriteLine($"TimelyApiService INFO: {message}");
            }
            catch
            {
                // Don't throw errors from logging
                System.Diagnostics.Debug.WriteLine($"Failed to log info: {message}");
            }
        }

        private string GetErrorLogPath()
        {
            var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var logDir = Path.Combine(documentsPath, "VirtualDesktopLogs");
            
            if (!Directory.Exists(logDir))
            {
                Directory.CreateDirectory(logDir);
            }
            
            return Path.Combine(logDir, "errors.log");
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _httpClient?.Dispose();
                }
                _disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }

    /// <summary>
    /// Result of a Timely upload operation
    /// </summary>
    public class TimelyUploadResult
    {
        public int SuccessCount { get; set; } = 0;
        public List<string> Errors { get; set; } = new List<string>();
        
        public bool HasErrors => Errors.Any();
        public int FailureCount => Errors.Count;
        public bool Success => !HasErrors && SuccessCount > 0;
    }
}
