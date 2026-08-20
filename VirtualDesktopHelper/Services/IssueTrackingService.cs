using System.Text.RegularExpressions;
using VirtualDesktopHelper.Configuration;

namespace VirtualDesktopHelper.Services
{
    /// <summary>
    /// Service for handling issue tracking integration functionality.
    /// </summary>
    public class IssueTrackingService
    {
        private readonly TrackerConfiguration _config;

        public IssueTrackingService(TrackerConfiguration? config = null)
        {
            _config = config ?? TrackerConfiguration.Instance;
        }

        /// <summary>
        /// Extracts the first issue identifier from a desktop name using the configured regex pattern.
        /// </summary>
        /// <param name="desktopName">The desktop name to search for issue identifiers.</param>
        /// <returns>The first issue identifier found, or null if none found.</returns>
        public string? ExtractIssueFromDesktopName(string desktopName)
        {
            if (string.IsNullOrWhiteSpace(desktopName) || 
                string.IsNullOrWhiteSpace(_config.IssueFormatRegex) ||
                !_config.EnableIssueTracking)
            {
                return null;
            }

            try
            {
                var regex = new Regex(_config.IssueFormatRegex);
                var match = regex.Match(desktopName);
                
                if (match.Success)
                {
                    return match.Value;
                }
            }
            catch (Exception)
            {
                // Invalid regex pattern, return null
                return null;
            }

            return null;
        }

        /// <summary>
        /// Generates the full URL for an issue identifier using the configured URL template.
        /// </summary>
        /// <param name="issueId">The issue identifier to generate a URL for.</param>
        /// <returns>The full issue URL, or null if URL template is not configured.</returns>
        public string? GenerateIssueUrl(string issueId)
        {
            if (string.IsNullOrWhiteSpace(issueId) || 
                string.IsNullOrWhiteSpace(_config.IssueUrlTemplate) ||
                !_config.EnableIssueTracking)
            {
                return null;
            }

            try
            {
                return string.Format(_config.IssueUrlTemplate, issueId);
            }
            catch (Exception)
            {
                // Invalid URL template format, return null
                return null;
            }
        }

        /// <summary>
        /// Extracts an issue identifier from a desktop name and generates its URL.
        /// </summary>
        /// <param name="desktopName">The desktop name to extract issue from.</param>
        /// <returns>The full issue URL, or null if no issue found or URL template not configured.</returns>
        public string? GetIssueUrlFromDesktopName(string desktopName)
        {
            var issueId = ExtractIssueFromDesktopName(desktopName);
            if (issueId == null)
            {
                return null;
            }

            return GenerateIssueUrl(issueId);
        }

        /// <summary>
        /// Checks if issue tracking is properly configured.
        /// </summary>
        /// <returns>True if issue tracking is enabled and properly configured, false otherwise.</returns>
        public bool IsConfigured()
        {
            return _config.EnableIssueTracking &&
                   !string.IsNullOrWhiteSpace(_config.IssueFormatRegex) &&
                   !string.IsNullOrWhiteSpace(_config.IssueUrlTemplate);
        }

        /// <summary>
        /// Validates the issue format regex pattern.
        /// </summary>
        /// <param name="pattern">The regex pattern to validate.</param>
        /// <returns>True if the pattern is valid, false otherwise.</returns>
        public static bool IsValidRegexPattern(string pattern)
        {
            if (string.IsNullOrWhiteSpace(pattern))
            {
                return false;
            }

            try
            {
                _ = new Regex(pattern);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Validates the issue URL template.
        /// </summary>
        /// <param name="template">The URL template to validate.</param>
        /// <returns>True if the template is valid, false otherwise.</returns>
        public static bool IsValidUrlTemplate(string template)
        {
            if (string.IsNullOrWhiteSpace(template))
            {
                return false;
            }

            try
            {
                // Test the template with a dummy issue ID
                var testUrl = string.Format(template, "TEST-123");
                return Uri.TryCreate(testUrl, UriKind.Absolute, out Uri? uri) &&
                       (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
