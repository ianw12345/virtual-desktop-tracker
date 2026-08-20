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
    /// Service for fetching project information from the Timely API.
    /// </summary>
    public class TimelyProjectService : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly TimelyConfiguration _timelyConfig;
        private bool _disposed = false;

        public TimelyProjectService()
        {
            _timelyConfig = TimelyConfiguration.Instance;
            _httpClient = TimelyHttpClientFactory.Create(_timelyConfig);
        }

        /// <summary>
        /// Fetches all active projects from the Timely API.
        /// </summary>
        /// <returns>List of active Timely projects, or empty list if an error occurs.</returns>
        public async Task<List<TimelyProject>> FetchProjectsAsync()
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

                // Construct the URL based on the example request
                var url = $"{_timelyConfig.ApiBaseUrl}/{_timelyConfig.WorkspaceId}/projects.json?limit=1000&offset=0&state=active&totals=false&version=3";

                var response = await _httpClient.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    {
                        throw new UnauthorizedAccessException("Authentication failed. Please update your Timely configuration with fresh authentication cookies.");
                    }
                    
                    throw new HttpRequestException($"Failed to fetch projects. Status: {response.StatusCode}, Reason: {response.ReasonPhrase}");
                }

                var jsonContent = await response.Content.ReadAsStringAsync();
                
                if (string.IsNullOrEmpty(jsonContent))
                {
                    throw new InvalidOperationException("Received empty response from Timely API.");
                }

                var projects = JsonSerializer.Deserialize<List<TimelyProject>>(jsonContent, TimelyHttpClientFactory.JsonOptions);
                
                if (projects == null)
                {
                    throw new InvalidOperationException("Failed to deserialize projects from Timely API response.");
                }

                // Filter to only active projects and sort by name
                var activeProjects = projects
                    .Where(p => p.Active)
                    .OrderBy(p => p.Client?.Name ?? "")
                    .ThenBy(p => p.Name)
                    .ToList();

                return activeProjects;
            }
            catch (Exception ex)
            {
                // Log the error but don't throw - let the UI handle it gracefully
                System.Diagnostics.Debug.WriteLine($"Error fetching Timely projects: {ex.Message}");
                throw; // Re-throw to let the caller handle the specific error
            }
        }

        /// <summary>
        /// Searches projects by name or client name.
        /// </summary>
        /// <param name="projects">List of projects to search.</param>
        /// <param name="searchTerm">Search term to filter by.</param>
        /// <returns>Filtered list of projects matching the search term.</returns>
        public static List<TimelyProject> SearchProjects(List<TimelyProject> projects, string searchTerm)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
            {
                return projects;
            }

            var lowerSearchTerm = searchTerm.ToLowerInvariant();
            
            return projects.Where(project =>
                project.Name.ToLowerInvariant().Contains(lowerSearchTerm) ||
                (project.Client?.Name?.ToLowerInvariant().Contains(lowerSearchTerm) ?? false) ||
                (project.Description?.ToLowerInvariant().Contains(lowerSearchTerm) ?? false) ||
                project.Id.ToString().Contains(lowerSearchTerm)
            ).ToList();
        }

        /// <summary>
        /// Tests the connection to Timely API with current configuration.
        /// </summary>
        /// <returns>True if connection is successful, false otherwise.</returns>
        public async Task<bool> TestConnectionAsync()
        {
            try
            {
                var projects = await FetchProjectsAsync();
                return projects.Count >= 0; // Even 0 projects means successful connection
            }
            catch
            {
                return false;
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
