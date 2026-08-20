using System;
using System.Collections.Generic;
using System.Linq;
using VirtualDesktopHelper.Configuration;
using VirtualDesktopHelper.Interfaces;
using VirtualDesktopHelper.Models;

namespace VirtualDesktopHelper.Services
{
    /// <summary>
    /// Service for estimating working hours and finish time based on desktop usage data.
    /// </summary>
    public class WorkingHoursEstimationService
    {
        private readonly TrackerConfiguration _config;
        private readonly IUsageConsolidationService _consolidationService;
        private const double StandardWorkHoursPerDay = 7.33; // 7h20m in decimal hours
        private const double LunchBreakMinThreshold = 20.0; // Minimum minutes for lunch break
        private readonly TimeOnly LunchTimeStart = new TimeOnly(11, 45); // 11:45 AM
        private readonly TimeOnly LunchTimeEnd = new TimeOnly(13, 15); // 1:15 PM
        private readonly TimeOnly LunchTimeTarget = new TimeOnly(12, 30); // 12:30 PM

        public WorkingHoursEstimationService(TrackerConfiguration? config = null, IUsageConsolidationService? consolidationService = null)
        {
            _config = config ?? TrackerConfiguration.Instance;
            _consolidationService = consolidationService ?? new UsageConsolidationService();
        }

        /// <summary>
        /// Estimates working hours and finish time for a given date.
        /// </summary>
        /// <param name="allEntries">All usage entries.</param>
        /// <param name="targetDate">The date to analyze (defaults to today).</param>
        /// <returns>Working hours estimation result.</returns>
        public WorkingHoursEstimation EstimateWorkingHours(List<DesktopUsageEntry> allEntries, DateTime? targetDate = null)
        {
            var date = targetDate ?? DateTime.Today;
            
            // Filter entries for the target date and exclude entire entries that start before 6:00 AM
            var dayEntries = allEntries
                .Where(e => e.StartTime.Date == date.Date)
                .Where(e => e.StartTime.TimeOfDay >= TimeSpan.FromHours(6))
                .OrderBy(e => e.StartTime)
                .ToList();


            var consolidatedEntries = _consolidationService.ConsolidateUsageEntries(dayEntries);

            if (!consolidatedEntries.Any())
            {
                return new WorkingHoursEstimation
                {
                    Date = date,
                    TotalWorkedHours = 0,
                    EstimatedFinishTime = null,
                    HoursRemaining = StandardWorkHoursPerDay,
                    LunchBreak = null,
                    Message = "No work activities found for this date (entries before 6:00 AM are ignored)."
                };
            }

            // Find lunch break
            var lunchBreak = FindLunchBreak(consolidatedEntries);
            
            // Calculate total working time (excluding lunch break)
            var totalWorkingTime = CalculateWorkingTime(consolidatedEntries, lunchBreak);
            var totalWorkedHours = totalWorkingTime.TotalHours;

            // Determine if still working (last entry is ongoing or recent)
            var lastEntry = consolidatedEntries.LastOrDefault();
            var isStillWorking = IsStillWorking(lastEntry);
            
            // Calculate finish time estimation
            var remainingHours = StandardWorkHoursPerDay - totalWorkedHours;
            var hoursRemaining = remainingHours <= 0.000001 ? 0 : remainingHours;
            DateTime? estimatedFinishTime = null;
            
            if (isStillWorking && hoursRemaining > 0)
            {
                // If still working, estimate finish time based on current time + remaining hours
                estimatedFinishTime = DateTime.Now.AddHours(hoursRemaining);
            }
            else if (hoursRemaining == 0)
            {
                // Already worked enough hours
                estimatedFinishTime = lastEntry?.EndTime ?? DateTime.Now;
            }

            return new WorkingHoursEstimation
            {
                Date = date,
                TotalWorkedHours = totalWorkedHours,
                EstimatedFinishTime = estimatedFinishTime,
                HoursRemaining = hoursRemaining,
                LunchBreak = lunchBreak,
                Message = GenerateEstimationMessage(totalWorkedHours, hoursRemaining, isStillWorking, lunchBreak)
            };
        }

        /// <summary>
        /// Finds the lunch break in the daily entries.
        /// </summary>
        private LunchBreakInfo? FindLunchBreak(List<DesktopUsageEntry> dayEntries)
        {
            // Look for "Screen Off" entries that overlap with the lunch window (11:45-13:15) and are longer than 20 minutes
            var lunchTimeStartSpan = LunchTimeStart.ToTimeSpan();
            var lunchTimeEndSpan = LunchTimeEnd.ToTimeSpan();
            
            var lunchCandidates = dayEntries
                .Where(e => e.DesktopName == "Screen Off" && 
                           e.Duration.TotalMinutes >= LunchBreakMinThreshold &&
                           // Check if the entry overlaps with the lunch window
                           e.StartTime.TimeOfDay <= lunchTimeEndSpan && 
                           (e.EndTime?.TimeOfDay ?? TimeSpan.FromHours(24)) >= lunchTimeStartSpan)
                .ToList();

            if (!lunchCandidates.Any())
            {
                return null;
            }

            // Find the candidate closest to 12:30
			var lunchBreak = lunchCandidates
				.OrderBy(e => {
					var entryMiddle = e.StartTime.TimeOfDay.Add(TimeSpan.FromTicks(e.Duration.Ticks / 2));
					return Math.Abs((entryMiddle - LunchTimeTarget.ToTimeSpan()).TotalMinutes);
				})
				.First();

            return new LunchBreakInfo
            {
                StartTime = lunchBreak.StartTime,
                EndTime = lunchBreak.EndTime ?? DateTime.Now,
                Duration = lunchBreak.Duration,
                WasDetected = true
            };
        }

        /// <summary>
        /// Calculates total working time excluding lunch break.
        /// </summary>
        private TimeSpan CalculateWorkingTime(List<DesktopUsageEntry> dayEntries, LunchBreakInfo? lunchBreak)
        {
            var totalTime = TimeSpan.Zero;

            foreach (var entry in dayEntries)
            {
                // Skip "Screen Off" entries that are the lunch break
                if (lunchBreak != null && entry.DesktopName == "Screen Off" && 
                    entry.StartTime == lunchBreak.StartTime)
                {
                    continue;
                }

                // Skip other "Screen Off" entries (they're not work time)
                if (entry.DesktopName == "Screen Off")
                {
                    continue;
                }

                totalTime = totalTime.Add(entry.Duration);
            }

            return totalTime;
        }

        /// <summary>
        /// Determines if the user is still working based on the last entry.
        /// </summary>
        private bool IsStillWorking(DesktopUsageEntry? lastEntry)
        {
            if (lastEntry == null) return false;

            // If last entry has no end time, user is still working
            if (lastEntry.EndTime == null) return true;

            // If last entry ended recently (within 30 minutes) and wasn't "Screen Off", assume still working
            var timeSinceLastActivity = DateTime.Now - (lastEntry.EndTime ?? DateTime.Now);
            return timeSinceLastActivity.TotalMinutes <= 30 && lastEntry.DesktopName != "Screen Off";
        }

        /// <summary>
        /// Generates a user-friendly message about the working hours estimation.
        /// </summary>
        private string GenerateEstimationMessage(double totalWorkedHours, double hoursRemaining, bool isStillWorking, LunchBreakInfo? lunchBreak)
        {
            var message = $"Worked: {FormatHours(totalWorkedHours)} / {FormatHours(StandardWorkHoursPerDay)}\n";
            
            if (lunchBreak != null)
            {
                message += $"Lunch break detected: {lunchBreak.StartTime:HH:mm} - {lunchBreak.EndTime:HH:mm} ({FormatTimeSpan(lunchBreak.Duration)})\n";
            }
            else
            {
                message += "No lunch break detected (no Screen Off 20+ min between 11:45-13:15)\n";
            }

            if (hoursRemaining <= 0)
            {
                message += "✅ You've completed your working hours for today!";
            }
            else if (isStillWorking)
            {
                message += $"⏰ {FormatHours(hoursRemaining)} remaining";
            }
            else
            {
                message += $"📊 {FormatHours(hoursRemaining)} needed to complete today";
            }

            return message;
        }

        /// <summary>
        /// Formats hours in a user-friendly way (e.g., "7h 20m").
        /// </summary>
        private string FormatHours(double hours)
        {
            var totalMinutes = (int)(hours * 60);
            var h = totalMinutes / 60;
            var m = totalMinutes % 60;
            
            if (h == 0) return $"{m}m";
            if (m == 0) return $"{h}h";
            return $"{h}h {m}m";
        }

        /// <summary>
        /// Formats a TimeSpan in a user-friendly way.
        /// </summary>
        private string FormatTimeSpan(TimeSpan timeSpan)
        {
            var totalMinutes = (int)timeSpan.TotalMinutes;
            var h = totalMinutes / 60;
            var m = totalMinutes % 60;
            
            if (h == 0) return $"{m}m";
            if (m == 0) return $"{h}h";
            return $"{h}h {m}m";
        }
    }

    /// <summary>
    /// Contains the result of working hours estimation.
    /// </summary>
    public class WorkingHoursEstimation
    {
        public DateTime Date { get; set; }
        public double TotalWorkedHours { get; set; }
        public DateTime? EstimatedFinishTime { get; set; }
        public double HoursRemaining { get; set; }
        public LunchBreakInfo? LunchBreak { get; set; }
        public string Message { get; set; } = "";
    }

    /// <summary>
    /// Contains information about the detected lunch break.
    /// </summary>
    public class LunchBreakInfo
    {
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public TimeSpan Duration { get; set; }
        public bool WasDetected { get; set; }
    }
}
