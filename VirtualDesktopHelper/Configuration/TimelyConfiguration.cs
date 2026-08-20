using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace VirtualDesktopHelper.Configuration
{
    /// <summary>
    /// Configuration settings for Timely integration.
    /// Stores sensitive information in a separate config file.
    /// </summary>
    public class TimelyConfiguration
    {
        private static TimelyConfiguration? _instance;
        private static readonly object _lock = new object();

        /// <summary>
        /// Timely API base URL
        /// </summary>
        public string ApiBaseUrl { get; set; } = "https://app.timelyapp.com";

        /// <summary>
        /// Timely workspace ID (from URL)
        /// </summary>
        public string WorkspaceId { get; set; } = "";

        /// <summary>
        /// Default Timely project ID for time entries (used when no specific project is detected)
        /// </summary>
        public long DefaultProjectId { get; set; }

        /// <summary>
        /// Timely user ID
        /// </summary>
        public long UserId { get; set; }

        /// <summary>
        /// CSRF token for API requests (needs to be updated periodically)
        /// </summary>
        public string CsrfToken { get; set; } = "";

        /// <summary>
        /// Socket ID for API requests
        /// </summary>
        public string SocketId { get; set; } = "";

        /// <summary>
        /// Cookie string for authentication (needs to be updated periodically)
        /// </summary>
        public string CookieString { get; set; } = "";

        /// <summary>
        /// Default timezone offset for timestamps
        /// </summary>
        public string TimezoneOffset { get; set; } = "+02:00";

        /// <summary>
        /// Filename for storing Timely configuration
        /// </summary>
        public static string ConfigFileName { get; set; } = "timely_config.json";

        /// <summary>
        /// Gets the singleton instance of TimelyConfiguration.
        /// </summary>
        public static TimelyConfiguration Instance
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
        /// Gets the full path to the configuration file.
        /// </summary>
        public static string GetConfigFilePath()
        {
            string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            return Path.Combine(documentsPath, "VirtualDesktopLogs", ConfigFileName);
        }

        /// <summary>
        /// Loads configuration from file or creates default configuration.
        /// </summary>
        private static TimelyConfiguration LoadConfiguration()
        {
            try
            {
                string configPath = GetConfigFilePath();
                if (File.Exists(configPath))
                {
                    string json = File.ReadAllText(configPath);
                    var persistedConfig = JsonSerializer.Deserialize<PersistedTimelyConfiguration>(json);
                    return persistedConfig?.ToConfiguration() ?? new TimelyConfiguration();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading Timely configuration: {ex.Message}");
            }

            return new TimelyConfiguration();
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

                string json = JsonSerializer.Serialize(ToPersistedConfiguration(), new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                File.WriteAllText(configPath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving Timely configuration: {ex.Message}");
            }
        }

        /// <summary>
        /// Checks if the essential configuration values are set.
        /// </summary>
        public bool IsConfigured()
        {
            return !string.IsNullOrEmpty(CsrfToken) && 
                   !string.IsNullOrEmpty(CookieString) &&
                   DefaultProjectId > 0 &&
                   UserId > 0;
        }

        /// <summary>
        /// Resets the instance to force reload from file.
        /// </summary>
        public static void Reset()
        {
            lock (_lock)
            {
                _instance = null;
            }
        }

        private PersistedTimelyConfiguration ToPersistedConfiguration() => new()
        {
            ApiBaseUrl = ApiBaseUrl,
            WorkspaceId = WorkspaceId,
            DefaultProjectId = DefaultProjectId,
            UserId = UserId,
            CsrfToken = DpapiSecretProtector.Protect(CsrfToken),
            SocketId = DpapiSecretProtector.Protect(SocketId),
            CookieString = DpapiSecretProtector.Protect(CookieString),
            TimezoneOffset = TimezoneOffset
        };

        private sealed class PersistedTimelyConfiguration
        {
            public string ApiBaseUrl { get; set; } = "https://app.timelyapp.com";
            public string WorkspaceId { get; set; } = "";
            public long DefaultProjectId { get; set; }
            public long UserId { get; set; }
            public string CsrfToken { get; set; } = "";
            public string SocketId { get; set; } = "";
            public string CookieString { get; set; } = "";
            public string TimezoneOffset { get; set; } = "+02:00";

            public TimelyConfiguration ToConfiguration() => new()
            {
                ApiBaseUrl = ApiBaseUrl,
                WorkspaceId = WorkspaceId,
                DefaultProjectId = DefaultProjectId,
                UserId = UserId,
                CsrfToken = DpapiSecretProtector.Unprotect(CsrfToken),
                SocketId = DpapiSecretProtector.Unprotect(SocketId),
                CookieString = DpapiSecretProtector.Unprotect(CookieString),
                TimezoneOffset = TimezoneOffset
            };
        }
    }
}
