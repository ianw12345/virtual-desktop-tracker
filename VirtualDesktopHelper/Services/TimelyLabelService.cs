using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using VirtualDesktopHelper.Configuration;
using VirtualDesktopHelper.Models;

namespace VirtualDesktopHelper.Services
{
    /// <summary>
    /// Service for fetching label information from the Timely API.
    /// </summary>
    public class TimelyLabelService : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly TimelyConfiguration _timelyConfig;
        private bool _disposed = false;

        public TimelyLabelService()
        {
            _timelyConfig = TimelyConfiguration.Instance;
            _httpClient = TimelyHttpClientFactory.Create(_timelyConfig);
        }

        /// <summary>
        /// Fetches all labels from the Timely API.
        /// </summary>
        /// <returns>List of Timely labels organized as a hierarchy.</returns>
        public async Task<List<TimelyLabel>> FetchLabelsAsync()
        {
            try
            {
                if (string.IsNullOrEmpty(_timelyConfig.WorkspaceId))
                {
                    throw new InvalidOperationException("Workspace ID is not configured. Please configure Timely settings first.");
                }

                if (string.IsNullOrEmpty(_timelyConfig.CookieString))
                {
                    throw new InvalidOperationException("Authentication cookies are not configured. Please configure Timely settings first.");
                }

                // Construct the URL for fetching labels
                var url = $"{_timelyConfig.ApiBaseUrl}/{_timelyConfig.WorkspaceId}/labels.json?filter=all&limit=500&offset=0";

                var response = await _httpClient.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    {
                        throw new UnauthorizedAccessException("Authentication failed. Please update your Timely configuration with fresh authentication cookies.");
                    }
                    
                    throw new HttpRequestException($"Failed to fetch labels. Status: {response.StatusCode}, Reason: {response.ReasonPhrase}");
                }

                var jsonContent = await response.Content.ReadAsStringAsync();
                
                if (string.IsNullOrEmpty(jsonContent))
                {
                    throw new InvalidOperationException("Received empty response from Timely API.");
                }

                var labels = JsonSerializer.Deserialize<List<TimelyLabel>>(jsonContent, TimelyHttpClientFactory.JsonOptions);
                
                if (labels == null)
                {
                    throw new InvalidOperationException("Failed to deserialize labels from Timely API response.");
                }

                // Build the full path for each label
                BuildLabelPaths(labels);

                // Filter to only active labels and sort by sequence/name
                var activeLabels = labels
                    .Where(l => l.Active)
                    .OrderBy(l => l.Sequence)
                    .ThenBy(l => l.Name)
                    .ToList();

                return activeLabels;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error fetching Timely labels: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Fetches detailed project information including label requirements.
        /// </summary>
        /// <param name="projectId">The ID of the project to fetch details for.</param>
        /// <returns>Detailed project information.</returns>
        public async Task<TimelyProjectDetails> FetchProjectDetailsAsync(long projectId)
        {
            try
            {
                if (string.IsNullOrEmpty(_timelyConfig.WorkspaceId))
                {
                    throw new InvalidOperationException("Workspace ID is not configured. Please configure Timely settings first.");
                }

                if (string.IsNullOrEmpty(_timelyConfig.CookieString))
                {
                    throw new InvalidOperationException("Authentication cookies are not configured. Please configure Timely settings first.");
                }

                // Construct the URL for fetching project details
                var url = $"{_timelyConfig.ApiBaseUrl}/{_timelyConfig.WorkspaceId}/projects/{projectId}.json";

                var response = await _httpClient.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    {
                        throw new UnauthorizedAccessException("Authentication failed. Please update your Timely configuration with fresh authentication cookies.");
                    }
                    
                    throw new HttpRequestException($"Failed to fetch project details. Status: {response.StatusCode}, Reason: {response.ReasonPhrase}");
                }

                var jsonContent = await response.Content.ReadAsStringAsync();
                
                if (string.IsNullOrEmpty(jsonContent))
                {
                    throw new InvalidOperationException("Received empty response from Timely API.");
                }

                var projectDetails = JsonSerializer.Deserialize<TimelyProjectDetails>(jsonContent, TimelyHttpClientFactory.JsonOptions);
                
                if (projectDetails == null)
                {
                    throw new InvalidOperationException("Failed to deserialize project details from Timely API response.");
                }

                return projectDetails;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error fetching Timely project details: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Gets the labels that are available for a specific project.
        /// </summary>
        /// <param name="projectDetails">The detailed project information.</param>
        /// <param name="allLabels">All available labels.</param>
        /// <returns>List of labels that can be used with this project.</returns>
        public List<TimelyLabel> GetProjectLabels(TimelyProjectDetails projectDetails, List<TimelyLabel> allLabels)
        {
            if (projectDetails.LabelIds == null || projectDetails.LabelIds.Count == 0)
            {
                return new List<TimelyLabel>();
            }

            var projectLabelIds = new HashSet<long>(projectDetails.LabelIds);
            var projectLabels = new List<TimelyLabel>();

            // Recursively find all labels and their children that match the project's label IDs
            FindMatchingLabels(allLabels, projectLabelIds, projectLabels);

            return projectLabels
                .OrderBy(l => l.Sequence)
                .ThenBy(l => l.Name)
                .ToList();
        }

        /// <summary>
        /// Builds full paths for labels based on their hierarchy.
        /// </summary>
        private void BuildLabelPaths(List<TimelyLabel> labels)
        {
            var labelDict = labels.ToDictionary(l => l.Id, l => l);

            foreach (var label in labels)
            {
                if (label.ParentId.HasValue && labelDict.TryGetValue(label.ParentId.Value, out var parent))
                {
                    label.FullPath = $"{parent.Name} → {label.Name}";
                }
                else
                {
                    label.FullPath = label.Name;
                }
            }
        }

        /// <summary>
        /// Recursively finds all labels that match the project's label IDs.
        /// </summary>
        private void FindMatchingLabels(List<TimelyLabel> allLabels, HashSet<long> projectLabelIds, List<TimelyLabel> result)
        {
            foreach (var label in allLabels)
            {
                if (projectLabelIds.Contains(label.Id))
                {
                    result.Add(label);
                }

                if (label.Children.Count > 0)
                {
                    FindMatchingLabels(label.Children, projectLabelIds, result);
                }
            }
        }

        /// <summary>
        /// Searches labels by name.
        /// </summary>
        /// <param name="labels">List of labels to search.</param>
        /// <param name="searchTerm">Search term to filter by.</param>
        /// <returns>Filtered list of labels matching the search term.</returns>
        public static List<TimelyLabel> SearchLabels(List<TimelyLabel> labels, string searchTerm)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
            {
                return labels;
            }

            var lowerSearchTerm = searchTerm.ToLowerInvariant();
            var result = new List<TimelyLabel>();

            SearchLabelsRecursive(labels, lowerSearchTerm, result);

            return result;
        }

        /// <summary>
        /// Recursively searches labels including children.
        /// </summary>
        private static void SearchLabelsRecursive(List<TimelyLabel> labels, string searchTerm, List<TimelyLabel> result)
        {
            foreach (var label in labels)
            {
                if (label.Name.ToLowerInvariant().Contains(searchTerm) ||
                    label.FullPath.ToLowerInvariant().Contains(searchTerm))
                {
                    result.Add(label);
                }

                if (label.Children.Count > 0)
                {
                    SearchLabelsRecursive(label.Children, searchTerm, result);
                }
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _httpClient?.Dispose();
                _disposed = true;
            }
        }
    }
}
