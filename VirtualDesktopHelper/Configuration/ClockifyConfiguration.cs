using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace VirtualDesktopHelper.Configuration
{
    /// <summary>
    /// Stores the connection and default-entry settings for Clockify.
    /// The API key is persisted using the current Windows user's DPAPI context.
    /// </summary>
    public class ClockifyConfiguration
    {
        private static ClockifyConfiguration? _instance;
        private static readonly object Lock = new object();

        public string ApiBaseUrl { get; set; } = "https://api.clockify.me/api/v1";
        public string ApiKey { get; set; } = "";
        public string WorkspaceId { get; set; } = "";
        public string DefaultProjectId { get; set; } = "";
        public string DefaultProjectName { get; set; } = "";
        public bool IsBillable { get; set; }
        public List<ClockifyProjectMapping> ProjectMappings { get; set; } = new List<ClockifyProjectMapping>();

        public static string ConfigFileName { get; set; } = "clockify_config.json";

        public static ClockifyConfiguration Instance
        {
            get
            {
                if (_instance != null)
                {
                    return _instance;
                }

                lock (Lock)
                {
                    _instance ??= LoadConfiguration();
                    return _instance;
                }
            }
        }

        public static string GetConfigFilePath()
        {
            string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            return Path.Combine(documentsPath, "VirtualDesktopLogs", ConfigFileName);
        }

        public bool IsConfigured() =>
            !string.IsNullOrWhiteSpace(ApiKey) &&
            !string.IsNullOrWhiteSpace(WorkspaceId) &&
            !string.IsNullOrWhiteSpace(DefaultProjectId);

        public string GetProjectIdForDesktop(string desktopName)
        {
            foreach (ClockifyProjectMapping mapping in ProjectMappings.OrderBy(mapping => mapping.Order))
            {
                if (mapping.Matches(desktopName))
                {
                    return mapping.ProjectId;
                }
            }

            return DefaultProjectId;
        }

        public void SaveConfiguration()
        {
            try
            {
                string configPath = GetConfigFilePath();
                string? directory = Path.GetDirectoryName(configPath);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var persisted = new PersistedClockifyConfiguration
                {
                    ApiBaseUrl = ApiBaseUrl,
                    ApiKey = DpapiSecretProtector.Protect(ApiKey),
                    WorkspaceId = WorkspaceId,
                    DefaultProjectId = DefaultProjectId,
                    DefaultProjectName = DefaultProjectName,
                    IsBillable = IsBillable,
                    ProjectMappings = ProjectMappings
                };
                File.WriteAllText(configPath, JsonSerializer.Serialize(persisted, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving Clockify configuration: {ex.Message}");
            }
        }

        public static void Reset()
        {
            lock (Lock)
            {
                _instance = null;
            }
        }

        private static ClockifyConfiguration LoadConfiguration()
        {
            try
            {
                string configPath = GetConfigFilePath();
                if (!File.Exists(configPath))
                {
                    return new ClockifyConfiguration();
                }

                string json = File.ReadAllText(configPath);
                return JsonSerializer.Deserialize<PersistedClockifyConfiguration>(json)?.ToConfiguration() ?? new ClockifyConfiguration();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading Clockify configuration: {ex.Message}");
                return new ClockifyConfiguration();
            }
        }

        private sealed class PersistedClockifyConfiguration
        {
            public string ApiBaseUrl { get; set; } = "https://api.clockify.me/api/v1";
            public string ApiKey { get; set; } = "";
            public string WorkspaceId { get; set; } = "";
            public string DefaultProjectId { get; set; } = "";
            public string DefaultProjectName { get; set; } = "";
            public bool IsBillable { get; set; }
            public List<ClockifyProjectMapping> ProjectMappings { get; set; } = new List<ClockifyProjectMapping>();

            public ClockifyConfiguration ToConfiguration() => new ClockifyConfiguration
            {
                ApiBaseUrl = ApiBaseUrl,
                ApiKey = DpapiSecretProtector.Unprotect(ApiKey),
                WorkspaceId = WorkspaceId,
                DefaultProjectId = DefaultProjectId,
                DefaultProjectName = DefaultProjectName,
                IsBillable = IsBillable,
                ProjectMappings = ProjectMappings ?? new List<ClockifyProjectMapping>()
            };
        }
    }

    /// <summary>Maps desktop-name keywords to a Clockify project.</summary>
    public class ClockifyProjectMapping
    {
        public string ProjectId { get; set; } = "";
        public string ProjectName { get; set; } = "";
        public List<string> Keywords { get; set; } = new List<string>();
        public int Order { get; set; }

        public bool Matches(string desktopName) =>
            !string.IsNullOrWhiteSpace(desktopName) &&
            Keywords.Any(keyword => !string.IsNullOrWhiteSpace(keyword) && desktopName.Contains(keyword, StringComparison.OrdinalIgnoreCase));
    }
}
