using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using VirtualDesktopHelper.Configuration;
using VirtualDesktopHelper.Interfaces;
using VirtualDesktopHelper.Models;

namespace VirtualDesktopHelper.Services
{
    /// <summary>
    /// Provides the application-level usage analysis formerly hosted by the command-line project.
    /// Hosts receive structured results instead of having to parse console output.
    /// </summary>
    public sealed class UsageAnalysisService
    {
        private readonly IDesktopUsageTracker _usageTracker;
        private readonly TrackerConfiguration _configuration;

        public UsageAnalysisService(IDesktopUsageTracker usageTracker, TrackerConfiguration? configuration = null)
        {
            _usageTracker = usageTracker ?? throw new ArgumentNullException(nameof(usageTracker));
            _configuration = configuration ?? TrackerConfiguration.Instance;
        }

        public WorkingHoursEstimation EstimateWorkingHours(DateTime date)
        {
            var estimationService = new WorkingHoursEstimationService(_configuration);
            return estimationService.EstimateWorkingHours(_usageTracker.GetAllUsageHistory(), date.Date);
        }

        public async Task<UsageReportResult> GenerateReportAsync(UsageReportOptions options)
        {
            ValidateOptions(options);

            List<DesktopUsageEntry> entries = _usageTracker
                .GetAllUsageHistory()
                .Where(entry => entry.StartTime.Date == options.Date.Date)
                .Select(CloneEntry)
                .ToList();

            if (entries.Count == 0)
            {
                return UsageReportResult.Empty(options.Date);
            }

            var reportConfiguration = new TrackerConfiguration
            {
                EnableActivityConsolidation = options.EnableConsolidation,
                ConsolidationMinDurationMinutes = options.MinimumDurationMinutes,
                CustomConsolidationMaxDurationMinutes = options.MaximumCustomDurationMinutes,
                EnableConsecutiveMerging = _configuration.EnableConsecutiveMerging,
                EnableCustomConsolidation = _configuration.EnableCustomConsolidation
            };

            string reportPath = Path.Combine(
                _usageTracker.GetLogDirectory(),
                $"usage_report_{options.Date:yyyy-MM-dd}.txt");
            var reportGenerator = new UsageReportGenerator(reportConfiguration);
            string report = await reportGenerator.GenerateReportWithJsonAsync(entries, reportPath);

            return new UsageReportResult(
                options.Date,
                entries.Count,
                report,
                reportPath,
                Path.ChangeExtension(reportPath, ".json"));
        }

        private static DesktopUsageEntry CloneEntry(DesktopUsageEntry entry) => new()
        {
            DesktopName = entry.DesktopName,
            StartTime = entry.StartTime,
            EndTime = entry.EndTime
        };

        private static void ValidateOptions(UsageReportOptions options)
        {
            if (options.MinimumDurationMinutes < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(options), "The minimum duration cannot be negative.");
            }

            if (options.MaximumCustomDurationMinutes <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(options), "The maximum custom duration must be greater than zero.");
            }
        }
    }

    public sealed record UsageReportOptions(
        DateTime Date,
        bool EnableConsolidation = true,
        double MinimumDurationMinutes = 2.0,
        double MaximumCustomDurationMinutes = 15.0);

    public sealed record UsageReportResult(
        DateTime Date,
        int EntryCount,
        string Report,
        string TextReportPath,
        string JsonReportPath)
    {
        public bool HasData => EntryCount > 0;

        public static UsageReportResult Empty(DateTime date) => new(date, 0, string.Empty, string.Empty, string.Empty);
    }
}
