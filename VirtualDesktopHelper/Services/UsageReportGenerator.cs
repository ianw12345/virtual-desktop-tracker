using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using VirtualDesktopHelper.Configuration;
using VirtualDesktopHelper.Interfaces;
using VirtualDesktopHelper.Models;

namespace VirtualDesktopHelper.Services
{
    /// <summary>
    /// Generates comprehensive usage reports from desktop usage data.
    /// </summary>
    public class UsageReportGenerator
    {
        private readonly TrackerConfiguration _config;
        private readonly IUsageConsolidationService _consolidationService;

        public UsageReportGenerator(TrackerConfiguration? config = null, IUsageConsolidationService? consolidationService = null)
        {
            _config = config ?? TrackerConfiguration.Instance;
            _consolidationService = consolidationService ?? new UsageConsolidationService(_config);
        }

        /// <summary>
        /// Generates a comprehensive usage report from the provided usage entries.
        /// </summary>
        /// <param name="allEntries">All usage entries across all sessions.</param>
        /// <param name="currentDayOnly">If true, only includes entries from the current day.</param>
        /// <returns>Formatted report as a string.</returns>
        public string GenerateReport(List<DesktopUsageEntry> allEntries, bool currentDayOnly = false)
        {
            var report = new StringBuilder();
            
            // Filter entries for current day if requested
            var filteredEntries = currentDayOnly ? DesktopUsageUtilities.FilterCurrentDayEntries(allEntries) : allEntries;
            
            BuildReportHeader(report, currentDayOnly);
            
            if (!filteredEntries.Any())
            {
                string noDataMessage = currentDayOnly ? "No usage data available for today." : "No usage data available.";
                report.AppendLine(noDataMessage);
                return report.ToString();
            }

            // Apply consolidation if enabled
            var consolidatedEntries = _config.EnableActivityConsolidation 
                ? _consolidationService.ConsolidateUsageEntries(filteredEntries)
                : filteredEntries;

            // Add consolidation info to report header
            if (_config.EnableActivityConsolidation && consolidatedEntries.Count != filteredEntries.Count)
            {
                report.AppendLine($"Consolidation applied: {filteredEntries.Count} activities → {consolidatedEntries.Count} activities");
                report.AppendLine($"Settings: Min duration = {_config.ConsolidationMinDurationMinutes}min, " +
                                $"Custom max duration = {_config.CustomConsolidationMaxDurationMinutes}min");
                report.AppendLine();
            }

            var groupedByDesktop = GroupByDesktop(consolidatedEntries);
            var groupedByDate = GroupByDate(consolidatedEntries);

            BuildTotalTimeSection(report, groupedByDesktop);
            BuildDailyBreakdownSection(report, groupedByDate);

            return report.ToString();
        }

        /// <summary>
        /// Generates both text and JSON reports from the provided usage entries.
        /// </summary>
        /// <param name="allEntries">All usage entries across all sessions.</param>
        /// <param name="reportFilePath">Path for the text report file.</param>
        /// <param name="currentDayOnly">If true, only includes entries from the current day.</param>
        /// <returns>Formatted report as a string.</returns>
        public async Task<string> GenerateReportWithJsonAsync(List<DesktopUsageEntry> allEntries, string reportFilePath, bool currentDayOnly = false)
        {
            // Ensure all entries have proper end times before processing
            DateTime now = DateTime.Now;
            foreach (var entry in allEntries.Where(e => e.EndTime == null))
            {
                entry.EndTime = now;
            }

            // Generate text report using existing method
            var textReport = GenerateReport(allEntries, currentDayOnly);
            
            // Save text report
            await File.WriteAllTextAsync(reportFilePath, textReport);
            
            // Generate consolidated entries for JSON
            var filteredEntries = currentDayOnly ? DesktopUsageUtilities.FilterCurrentDayEntries(allEntries) : allEntries;
            var consolidatedEntries = _config.EnableActivityConsolidation 
                ? _consolidationService.ConsolidateUsageEntries(filteredEntries)
                : filteredEntries;
            
            // Generate and save JSON report
            await GenerateJsonReportAsync(consolidatedEntries, reportFilePath);
            
            return textReport;
        }

        /// <summary>
        /// Generates a JSON file containing all consolidated activities.
        /// </summary>
        /// <param name="consolidatedEntries">The consolidated usage entries.</param>
        /// <param name="textReportFilePath">Path of the text report file (used to derive JSON filename).</param>
        private async Task GenerateJsonReportAsync(List<DesktopUsageEntry> consolidatedEntries, string textReportFilePath)
        {
            try
            {
                // Create JSON filename by replacing .txt with .json
                var jsonFilePath = Path.ChangeExtension(textReportFilePath, ".json");
                
                // For now, create a simple report without project detection due to circular dependencies
                // TODO: Fix circular dependency and add project detection later
                
                // Create JSON-friendly object with additional metadata
                var jsonReport = new
                {
                    GeneratedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    TotalActivities = consolidatedEntries.Count,
                    ConsolidationEnabled = _config.EnableActivityConsolidation,
                    ConsolidationSettings = new
                    {
                        MinDurationMinutes = _config.ConsolidationMinDurationMinutes,
                        CustomMaxDurationMinutes = _config.CustomConsolidationMaxDurationMinutes,
                        EnableConsecutiveMerging = _config.EnableConsecutiveMerging,
                        EnableCustomConsolidation = _config.EnableCustomConsolidation
                    },
                    Activities = consolidatedEntries.Select(entry => new
                    {
                        DesktopName = entry.DesktopName,
                        StartTime = entry.StartTime.ToString("yyyy-MM-dd HH:mm:ss"),
                        EndTime = entry.EndTime?.ToString("yyyy-MM-dd HH:mm:ss"),
                        DurationSeconds = (int)entry.Duration.TotalSeconds,
                        DurationMinutes = Math.Round(entry.Duration.TotalMinutes, 2),
                        DurationFormatted = DesktopUsageUtilities.FormatTimeSpan(entry.Duration),
                        Date = entry.StartTime.ToString("yyyy-MM-dd")
                    }).ToList()
                };
                
                // Serialize with pretty formatting
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true
                };
                
                var json = JsonSerializer.Serialize(jsonReport, options);
                await File.WriteAllTextAsync(jsonFilePath, json);
                
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error generating JSON report: {ex.Message}");
            }
        }

        private void BuildReportHeader(StringBuilder report, bool currentDayOnly = false)
        {
            string title = currentDayOnly ? "Virtual Desktop Usage Report - Today" : "Virtual Desktop Usage Report";
            report.AppendLine(title);
            report.AppendLine("Generated: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            if (currentDayOnly)
            {
                report.AppendLine("Report Date: " + DateTime.Today.ToString("yyyy-MM-dd"));
            }
            report.AppendLine(new string('=', 50));
            report.AppendLine();
        }

        private Dictionary<string, TimeSpan> GroupByDesktop(List<DesktopUsageEntry> allEntries)
        {
            var groupedByDesktop = new Dictionary<string, TimeSpan>();

            foreach (var entry in allEntries)
            {
                if (!groupedByDesktop.ContainsKey(entry.DesktopName))
                    groupedByDesktop[entry.DesktopName] = TimeSpan.Zero;

                groupedByDesktop[entry.DesktopName] = groupedByDesktop[entry.DesktopName].Add(entry.Duration);
            }

            return groupedByDesktop;
        }

        private Dictionary<string, List<DesktopUsageEntry>> GroupByDate(List<DesktopUsageEntry> allEntries)
        {
            var groupedByDate = new Dictionary<string, List<DesktopUsageEntry>>();

            foreach (var entry in allEntries)
            {
                string dateKey = entry.StartTime.ToString("yyyy-MM-dd");
                if (!groupedByDate.ContainsKey(dateKey))
                    groupedByDate[dateKey] = new List<DesktopUsageEntry>();

                groupedByDate[dateKey].Add(entry);
            }

            return groupedByDate;
        }

        private void BuildTotalTimeSection(StringBuilder report, Dictionary<string, TimeSpan> groupedByDesktop)
        {
            report.AppendLine("Total Time Per Desktop:");
            report.AppendLine(new string('-', 30));
            
            foreach (var kvp in groupedByDesktop.OrderByDescending(x => x.Value))
            {
                report.AppendLine($"{kvp.Key}: {DesktopUsageUtilities.FormatTimeSpan(kvp.Value)}");
            }
            report.AppendLine();
        }

        private void BuildDailyBreakdownSection(StringBuilder report, Dictionary<string, List<DesktopUsageEntry>> groupedByDate)
        {
            report.AppendLine("Daily Usage Breakdown:");
            report.AppendLine(new string('-', 30));
            
            foreach (var dateGroup in groupedByDate.OrderByDescending(x => x.Key))
            {
                report.AppendLine($"\n{dateGroup.Key}:");
                foreach (var entry in dateGroup.Value.OrderBy(x => x.StartTime))
                {
                    string endTimeStr = entry.EndTime?.ToString("HH:mm:ss") ?? "ongoing";
                    report.AppendLine($"  {entry.StartTime:HH:mm:ss} - {endTimeStr} : {entry.DesktopName} ({DesktopUsageUtilities.FormatTimeSpan(entry.Duration)})");
                }
            }
        }
    }
}
