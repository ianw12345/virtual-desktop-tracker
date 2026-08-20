using System;
using System.IO;
using System.Text.Json;

namespace VirtualDesktopHelper.Configuration
{
    /// <summary>
    /// Configuration settings for the Virtual Desktop Tracker application.
    /// </summary>
    public class TrackerConfiguration
    {
        private static TrackerConfiguration? _instance;
        private static readonly object _lock = new object();
        /// <summary>
        /// How often to check for desktop changes when screen is active.
        /// </summary>
        public TimeSpan ActiveScreenUpdateInterval { get; set; } = TimeSpan.FromSeconds(2);

        /// <summary>
        /// How often to check for desktop changes when screen is off/locked.
        /// </summary>
        public TimeSpan InactiveScreenUpdateInterval { get; set; } = TimeSpan.FromSeconds(10);

        /// <summary>
        /// Whether the global Back and Forward mouse buttons switch to the previous and next virtual desktop.
        /// When enabled, those button presses are consumed by this application while it is running.
        /// </summary>
        public bool EnableMouseDesktopNavigation { get; set; } = false;

        /// <summary>
        /// Start of the active user-controlled tracking session. It is persisted so an unexpected app restart
        /// does not silently discard the intended Clockify upload range.
        /// </summary>
        public DateTime? ClockifyCheckInStartedAt { get; set; }

        /// <summary>
        /// How long user must be idle before considering screen "off".
        /// </summary>
        public TimeSpan ScreenOffIdleThreshold { get; set; } = TimeSpan.FromMinutes(10);

        /// <summary>
        /// Margin from screen edge for display positioning (in pixels).
        /// </summary>
        public int DisplayMargin { get; set; } = 10;

        /// <summary>
        /// Approximate taskbar height for positioning calculations (in pixels).
        /// </summary>
        public int TaskbarHeight { get; set; } = 40;

        /// <summary>
        /// Directory name under Documents where logs are stored.
        /// </summary>
        public string LogDirectoryName { get; set; } = "VirtualDesktopLogs";

        /// <summary>
        /// Filename pattern for log files.
        /// </summary>
        public string LogFileNamePattern { get; set; } = "VirtualDesktopUsage_{0:yyyy-MM-dd_HH-mm-ss}.json";

        /// <summary>
        /// Filename for the usage report.
        /// </summary>
        public string ReportFileName { get; set; } = "VirtualDesktopUsageReport.txt";

        /// <summary>
        /// Maximum number of retry attempts for subprocess operations.
        /// </summary>
        public int SubprocessRetryCount { get; set; } = 3;

        /// <summary>
        /// Delay between retry attempts.
        /// </summary>
        public TimeSpan SubprocessRetryDelay { get; set; } = TimeSpan.FromMilliseconds(100);

        /// <summary>
        /// General retry delay for error handler operations.
        /// </summary>
        public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(1);

        /// <summary>
        /// Timeout for subprocess operations.
        /// </summary>
        public TimeSpan SubprocessTimeout { get; set; } = TimeSpan.FromSeconds(5);

        /// <summary>
        /// Minimum duration in minutes for an activity to be kept separate during consolidation.
        /// Activities shorter than this will be merged with adjacent larger activities.
        /// </summary>
        public double ConsolidationMinDurationMinutes { get; set; } = 2.0;

        /// <summary>
        /// Maximum duration in minutes for desktop name based consolidation.
        /// Desktop activities with names matching patterns and shorter than this duration will be consolidated.
        /// </summary>
        public double CustomConsolidationMaxDurationMinutes { get; set; } = 15.0;

        /// <summary>
        /// Whether to apply activity consolidation during report generation.
        /// </summary>
        public bool EnableActivityConsolidation { get; set; } = true;

        /// <summary>
        /// Whether to merge consecutive activities with the same desktop name.
        /// </summary>
        public bool EnableConsecutiveMerging { get; set; } = true;

        /// <summary>
        /// Whether to apply custom desktop name based consolidation rules.
        /// </summary>
        public bool EnableCustomConsolidation { get; set; } = true;

        /// <summary>
        /// Regular expression pattern to match issue identifiers in desktop names.
        /// Default pattern matches formats like "APP-5482", "PROJ-123", etc.
        /// </summary>
        public string IssueFormatRegex { get; set; } = @"\b[A-Z][A-Z0-9]+-\d+\b";

        /// <summary>
        /// URL template for issue links. Use {0} as placeholder for the issue identifier.
        /// Example: "https://www.issuetracker.com/browse/{0}"
        /// </summary>
        public string IssueUrlTemplate { get; set; } = "";

        /// <summary>
        /// Whether issue tracking integration is enabled.
        /// </summary>
        public bool EnableIssueTracking { get; set; } = false;

        /// <summary>
        /// Filename for storing tracker configuration.
        /// </summary>
        public static string ConfigFileName { get; set; } = "tracker_config.json";

        /// <summary>
        /// Gets the singleton instance of the configuration.
        /// </summary>
        public static TrackerConfiguration Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = LoadConfiguration();
                        }
                    }
                }
                return _instance;
            }
        }

        /// <summary>
        /// Gets the path to the configuration file.
        /// </summary>
        public static string GetConfigFilePath()
        {
            string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            return Path.Combine(documentsPath, "VirtualDesktopLogs", ConfigFileName);
        }

        /// <summary>
        /// Loads configuration from file or creates default configuration.
        /// </summary>
        private static TrackerConfiguration LoadConfiguration()
        {
            try
            {
                string configPath = GetConfigFilePath();
                if (File.Exists(configPath))
                {
                    string json = File.ReadAllText(configPath);
                    
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        WriteIndented = true
                    };
                    
                    var config = JsonSerializer.Deserialize<TrackerConfiguration>(json, options);
                    if (config != null)
                    {
                        return config;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading tracker configuration: {ex.Message}");
            }

            return new TrackerConfiguration();
        }

        /// <summary>
        /// Saves the current configuration to file.
        /// </summary>
        public void SaveConfiguration()
        {
            try
            {
                string configPath = GetConfigFilePath();
                string? directory = Path.GetDirectoryName(configPath);
                
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var options = new JsonSerializerOptions
                {
                    WriteIndented = true
                };

                string json = JsonSerializer.Serialize(this, options);
                File.WriteAllText(configPath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving tracker configuration: {ex.Message}");
            }
        }
    }
}
