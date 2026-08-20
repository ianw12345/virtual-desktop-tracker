using System;
using System.Collections.Generic;
using System.Linq;
using VirtualDesktopHelper.Configuration;
using VirtualDesktopHelper.Interfaces;
using VirtualDesktopHelper.Models;

namespace VirtualDesktopHelper.Services
{
    /// <summary>
    /// Service for consolidating desktop usage entries based on various criteria.
    /// Implements the consolidation logic previously in Python analysis.py.
    /// </summary>
    public class UsageConsolidationService : IUsageConsolidationService
    {
        private readonly TrackerConfiguration _config;

        public UsageConsolidationService(TrackerConfiguration? config = null)
        {
            _config = config ?? TrackerConfiguration.Instance;
        }

        /// <summary>
        /// Applies all enabled consolidation strategies to the usage entries.
        /// </summary>
        /// <param name="entries">The original usage entries.</param>
        /// <returns>Consolidated usage entries.</returns>
        public List<DesktopUsageEntry> ConsolidateUsageEntries(List<DesktopUsageEntry> entries)
        {
            if (!entries.Any() || !_config.EnableActivityConsolidation)
            {
                return entries.ToList(); // Return a copy to avoid modifying original
            }

            // Convert to working format that allows time manipulation
            var workingEntries = ConvertToWorkingFormat(entries);

            // Apply consolidation steps in order
            if (_config.EnableCustomConsolidation)
            {
                workingEntries = ApplyCustomConsolidationRules(workingEntries);
            }

            // Apply size-based consolidation last
            workingEntries = ConsolidateShortActivities(workingEntries);

            if (_config.EnableConsecutiveMerging)
            {
                workingEntries = MergeConsecutiveActivities(workingEntries);
            }

			// Convert back to DesktopUsageEntry format
			return ConvertFromWorkingFormat(workingEntries);
        }

        /// <summary>
        /// Consolidates activities based on size - smallest activities get consolidated first.
        /// Duration gets added to the larger of the adjacent activities.
        /// </summary>
        private List<WorkingUsageEntry> ConsolidateShortActivities(List<WorkingUsageEntry> entries)
        {
            if (!entries.Any())
                return entries;

            var workingList = entries.ToList();

            while (true)
            {
                // Find activities smaller than minimum duration
                var smallActivities = workingList
                    .Select((entry, index) => new { Entry = entry, Index = index })
                    .Where(x => x.Entry.DurationMinutes < _config.ConsolidationMinDurationMinutes)
                    .ToList();

                if (!smallActivities.Any())
                    break; // No more small activities to consolidate

                // Find the smallest activity
                var smallest = smallActivities.OrderBy(x => x.Entry.DurationMinutes).First();

                // Find adjacent activities
                var prevIndex = smallest.Index > 0 ? smallest.Index - 1 : -1;
                var nextIndex = smallest.Index < workingList.Count - 1 ? smallest.Index + 1 : -1;

                // Determine which adjacent activity to merge with (the larger one)
                int mergeWithIndex;
                if (prevIndex >= 0 && nextIndex >= 0)
                {
                    // Both adjacent activities exist, choose the larger one
                    var prevDuration = workingList[prevIndex].DurationMinutes;
                    var nextDuration = workingList[nextIndex].DurationMinutes;
                    mergeWithIndex = prevDuration >= nextDuration ? prevIndex : nextIndex;
                }
                else if (prevIndex >= 0)
                {
                    mergeWithIndex = prevIndex;
                }
                else if (nextIndex >= 0)
                {
                    mergeWithIndex = nextIndex;
                }
                else
                {
                    // No adjacent activities, keep this one
                    break;
                }

                // Merge the smallest activity with the chosen adjacent activity
                var target = workingList[mergeWithIndex];
                var source = smallest.Entry;

                System.Diagnostics.Debug.WriteLine($"Consolidating '{source.DesktopName}' ({source.DurationMinutes:F0}min) " +
                                                  $"into '{target.DesktopName}' ({target.DurationMinutes:F0}min)");

                if (mergeWithIndex < smallest.Index)
                {
                    // Merging with previous activity - extend its end time
                    target.DurationMinutes += source.DurationMinutes;
                    target.EndMinutes = source.EndMinutes;
                    target.EndTime = source.EndTime;
                }
                else
                {
                    // Merging with next activity - extend its start time backward
                    target.DurationMinutes += source.DurationMinutes;
                    target.StartMinutes = source.StartMinutes;
                    target.StartTime = source.StartTime;
                }

                // Remove the smallest activity
                workingList.RemoveAt(smallest.Index);

                System.Diagnostics.Debug.WriteLine($"Result: '{target.DesktopName}' now {target.DurationMinutes:F0}min");
            }

            return workingList;
        }

        /// <summary>
        /// Some custom, use case specific handling:
        /// - If desktop name is "Desktop n", where n is a number, and the time is less than configured duration, consolidate with the next record
        /// - If desktop name is "Screen Off" (and the duration is less than configured duration) consolidate with the record before it
        /// </summary>
        private List<WorkingUsageEntry> ApplyCustomConsolidationRules(List<WorkingUsageEntry> entries)
        {
            var workingList = entries.ToList();
            bool changesMade = true;

            while (changesMade)
            {
                changesMade = false;

                for (int i = workingList.Count - 1; i >= 0; i--) // Iterate backwards
                {
                    var current = workingList[i];

                    if ((IsDesktopNamePattern(current.DesktopName) || current.DesktopName == "General") &&
                        current.DurationMinutes < _config.CustomConsolidationMaxDurationMinutes)
                    {
                        if (i + 1 < workingList.Count)
                        {
                            var nextRecord = workingList[i + 1];
                            System.Diagnostics.Debug.WriteLine($"Consolidating '{current.DesktopName}' into '{nextRecord.DesktopName}'");
                            
                            nextRecord.StartMinutes = current.StartMinutes;
                            nextRecord.StartTime = current.StartTime;
                            nextRecord.DurationMinutes += current.DurationMinutes;
                            
                            workingList.RemoveAt(i);
                            changesMade = true;
                            break; // Start over from the end after making a change
                        }
                    }
                    else if (current.DesktopName == "Screen Off" &&
                             current.DurationMinutes < _config.CustomConsolidationMaxDurationMinutes)
                    {
                        if (i - 1 >= 0)
                        {
                            var prevRecord = workingList[i - 1];
                            System.Diagnostics.Debug.WriteLine($"Consolidating '{current.DesktopName}' into '{prevRecord.DesktopName}'");
                            
                            prevRecord.EndMinutes = current.EndMinutes;
                            prevRecord.EndTime = current.EndTime;
                            prevRecord.DurationMinutes += current.DurationMinutes;
                            
                            workingList.RemoveAt(i);
                            changesMade = true;
                            break; // Start over from the end after making a change
                        }
                    }
                }
            }

            return workingList;
        }

        /// <summary>
        /// Merge consecutive activities with the same desktop name.
        /// </summary>
        private List<WorkingUsageEntry> MergeConsecutiveActivities(List<WorkingUsageEntry> entries)
        {
            if (!entries.Any())
                return entries;

            // Sort by start time first
            var sortedEntries = entries.OrderBy(x => x.StartMinutes).ToList();
            var merged = new List<WorkingUsageEntry>();

            foreach (var entry in sortedEntries)
            {
                if (merged.Any() &&
                    merged.Last().DesktopName == entry.DesktopName &&
                    entry.StartTime >= merged.Last().EndTime)
                {
                    // Extend the previous record to include this one (exactly like Python version)
                    var prevRecord = merged.Last();
                    prevRecord.EndTime = entry.EndTime;
                    prevRecord.EndMinutes = entry.EndMinutes;
                    prevRecord.DurationMinutes = prevRecord.EndMinutes - prevRecord.StartMinutes;
                    
                    System.Diagnostics.Debug.WriteLine($"Merged consecutive '{entry.DesktopName}' activities: {prevRecord.StartTime:HH:mm:ss} - {prevRecord.EndTime:HH:mm:ss}");
                }
                else
                {
                    // Add as new record
                    merged.Add(new WorkingUsageEntry
                    {
                        DesktopName = entry.DesktopName,
                        StartTime = entry.StartTime,
                        EndTime = entry.EndTime,
                        StartMinutes = entry.StartMinutes,
                        EndMinutes = entry.EndMinutes,
                        DurationMinutes = entry.DurationMinutes
                    });
                }
            }

            return merged;
        }

        /// <summary>
        /// Checks if the desktop name matches the pattern "Desktop n" where n is a number.
        /// </summary>
        private static bool IsDesktopNamePattern(string desktopName)
        {
            if (string.IsNullOrEmpty(desktopName) || !desktopName.StartsWith("Desktop "))
                return false;

            var numberPart = desktopName.Substring("Desktop ".Length).Trim();
            return int.TryParse(numberPart, out _);
        }

        /// <summary>
        /// Convert DesktopUsageEntry to WorkingUsageEntry format for easier manipulation.
        /// </summary>
        private List<WorkingUsageEntry> ConvertToWorkingFormat(List<DesktopUsageEntry> entries)
        {
            return entries
                .Where(e => e.EndTime.HasValue) // Skip active entries for consolidation
                .Select(entry => new WorkingUsageEntry
                {
                    DesktopName = entry.DesktopName,
                    StartTime = entry.StartTime,
                    EndTime = entry.EndTime!.Value,
                    StartMinutes = GetMinutesFromMidnight(entry.StartTime),
                    EndMinutes = GetMinutesFromMidnight(entry.EndTime!.Value),
                    DurationMinutes = entry.Duration.TotalMinutes
                })
                .OrderBy(e => e.StartMinutes)
                .ToList();
        }

        /// <summary>
        /// Convert WorkingUsageEntry back to DesktopUsageEntry format.
        /// </summary>
        private List<DesktopUsageEntry> ConvertFromWorkingFormat(List<WorkingUsageEntry> workingEntries)
        {
            return workingEntries.Select(entry => new DesktopUsageEntry
            {
                DesktopName = entry.DesktopName,
                StartTime = entry.StartTime,
                EndTime = entry.EndTime
            }).ToList();
        }

        /// <summary>
        /// Get the number of minutes from midnight for a given DateTime.
        /// </summary>
        private static double GetMinutesFromMidnight(DateTime dateTime)
        {
            return dateTime.TimeOfDay.TotalMinutes;
        }

        /// <summary>
        /// Internal working class for easier time manipulation during consolidation.
        /// </summary>
        private class WorkingUsageEntry
        {
            public string DesktopName { get; set; } = "";
            public DateTime StartTime { get; set; }
            public DateTime EndTime { get; set; }
            public double StartMinutes { get; set; }
            public double EndMinutes { get; set; }
            public double DurationMinutes { get; set; }
        }
    }
}
