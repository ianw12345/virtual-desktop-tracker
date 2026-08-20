using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using VirtualDesktopHelper.Configuration;
using VirtualDesktopHelper.Interfaces;
using VirtualDesktopHelper.Models;

namespace VirtualDesktopHelper.Services
{
    /// <summary>
    /// Uploads recorded desktop usage to Clockify's public v1 API.
    /// </summary>
    public sealed class ClockifyApiService : IDisposable
    {
        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
        private readonly ClockifyConfiguration _configuration;
        private readonly HttpClient _httpClient;
        private readonly IUsageConsolidationService _consolidationService;
        private readonly bool _ownsHttpClient;

        public ClockifyApiService(
            ClockifyConfiguration? configuration = null,
            IUsageConsolidationService? consolidationService = null,
            HttpClient? httpClient = null)
        {
            _configuration = configuration ?? ClockifyConfiguration.Instance;
            _consolidationService = consolidationService ?? new UsageConsolidationService();
            _httpClient = httpClient ?? CreateHttpClient(_configuration);
            _ownsHttpClient = httpClient is null;
        }

        public async Task<List<ClockifyWorkspace>> GetWorkspacesAsync()
        {
            EnsureApiKeyIsConfigured();
            return await GetJsonAsync<List<ClockifyWorkspace>>("workspaces") ?? new List<ClockifyWorkspace>();
        }

        public async Task<List<ClockifyProject>> GetProjectsAsync(string workspaceId)
        {
            if (string.IsNullOrWhiteSpace(workspaceId))
            {
                throw new ArgumentException("A Clockify workspace is required.", nameof(workspaceId));
            }

            EnsureApiKeyIsConfigured();
            var projects = new List<ClockifyProject>();
            for (int page = 1; ; page++)
            {
                var response = await GetJsonAsync<List<ClockifyProject>>($"workspaces/{Uri.EscapeDataString(workspaceId)}/projects?archived=false&page={page}&page-size=100");
                if (response is null || response.Count == 0)
                {
                    break;
                }

                projects.AddRange(response.Where(project => !project.Archived));
                if (response.Count < 100)
                {
                    break;
                }
            }

            return projects.OrderBy(project => project.DisplayName, StringComparer.CurrentCultureIgnoreCase).ToList();
        }

        /// <summary>Verifies that the supplied key can access the configured workspace.</summary>
        public async Task TestConnectionAsync()
        {
            List<ClockifyWorkspace> workspaces = await GetWorkspacesAsync();
            if (!string.IsNullOrWhiteSpace(_configuration.WorkspaceId) &&
                !workspaces.Any(workspace => string.Equals(workspace.Id, _configuration.WorkspaceId, StringComparison.Ordinal)))
            {
                throw new InvalidOperationException("The configured Clockify workspace is not accessible with this API key.");
            }
        }

        public async Task<ClockifyUploadResult> UploadAsync(List<DesktopUsageEntry> allEntries, bool currentDayOnly = true, DateTime? fromTime = null)
        {
            var result = new ClockifyUploadResult();
            if (!_configuration.IsConfigured())
            {
                result.Errors.Add("Clockify configuration is incomplete. API key, workspace and default project are required.");
                return result;
            }

            List<DesktopUsageEntry> entries = DesktopUsageUtilities.EnsureEndTimesAreSet(allEntries);
            if (currentDayOnly)
            {
                entries = DesktopUsageUtilities.FilterCurrentDayEntries(entries);
            }

            if (fromTime.HasValue)
            {
                entries = DesktopUsageUtilities.FilterEntriesFromTime(entries, fromTime.Value);
            }

            List<DesktopUsageEntry> validEntries = DesktopUsageUtilities.FilterZeroDurationEntries(
                _consolidationService.ConsolidateUsageEntries(entries));
            if (!validEntries.Any())
            {
                result.Errors.Add("No usage data is available for the selected period.");
                return result;
            }

            foreach (DesktopUsageEntry entry in validEntries.OrderBy(entry => entry.StartTime))
            {
                try
                {
                    await CreateTimeEntryAsync(entry);
                    result.SuccessCount++;
                }
                catch (Exception ex)
                {
                    result.Errors.Add($"{entry.DesktopName}: {ex.Message}");
                }
            }

            return result;
        }

        private async Task CreateTimeEntryAsync(DesktopUsageEntry entry)
        {
            DateTime endTime = entry.EndTime ?? DateTime.Now;
            if (endTime <= entry.StartTime)
            {
                throw new InvalidOperationException("The time entry has no positive duration.");
            }

            var payload = new
            {
                start = ToUtcIsoString(entry.StartTime),
                end = ToUtcIsoString(endTime),
                description = $"Virtual desktop: {entry.DesktopName}",
                projectId = _configuration.GetProjectIdForDesktop(entry.DesktopName),
                billable = _configuration.IsBillable,
                type = "REGULAR"
            };

            using var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            using HttpResponseMessage response = await _httpClient.PostAsync($"workspaces/{Uri.EscapeDataString(_configuration.WorkspaceId)}/time-entries", content);
            if (!response.IsSuccessStatusCode)
            {
                string body = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"Clockify returned {(int)response.StatusCode} ({response.ReasonPhrase}). {body}".Trim());
            }
        }

        private async Task<T?> GetJsonAsync<T>(string requestUri)
        {
            using HttpResponseMessage response = await _httpClient.GetAsync(requestUri);
            if (!response.IsSuccessStatusCode)
            {
                string body = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"Clockify returned {(int)response.StatusCode} ({response.ReasonPhrase}). {body}".Trim());
            }

            await using var content = await response.Content.ReadAsStreamAsync();
            return await JsonSerializer.DeserializeAsync<T>(content, JsonOptions);
        }

        private static HttpClient CreateHttpClient(ClockifyConfiguration configuration)
        {
            var client = new HttpClient { BaseAddress = new Uri(configuration.ApiBaseUrl.TrimEnd('/') + "/") };
            client.DefaultRequestHeaders.Add("X-Api-Key", configuration.ApiKey);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            return client;
        }

        private void EnsureApiKeyIsConfigured()
        {
            if (string.IsNullOrWhiteSpace(_configuration.ApiKey))
            {
                throw new InvalidOperationException("A Clockify API key is required.");
            }
        }

        internal static string ToUtcIsoString(DateTime dateTime) => dateTime.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'");

        public void Dispose()
        {
            if (_ownsHttpClient)
            {
                _httpClient.Dispose();
            }
        }
    }

    /// <summary>Summary of one Clockify upload run.</summary>
    public sealed class ClockifyUploadResult
    {
        public int SuccessCount { get; set; }
        public List<string> Errors { get; } = new List<string>();
        public bool HasErrors => Errors.Count > 0;
        public int FailureCount => Errors.Count;
        public bool Success => SuccessCount > 0 && !HasErrors;
    }
}
