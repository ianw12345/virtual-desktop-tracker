using System;
using System.Collections.Generic;
using FluentAssertions;
using Moq;
using VirtualDesktopHelper.Interfaces;
using VirtualDesktopHelper.Models;
using VirtualDesktopHelper.Services;

namespace VirtualDesktopHelper.Tests.Services
{
    public class UsageAnalysisServiceTests
    {
        [Fact]
        public void EstimateWorkingHours_UsesTheRequestedDate()
        {
            var usageTracker = new Mock<IDesktopUsageTracker>();
            usageTracker.Setup(tracker => tracker.GetAllUsageHistory()).Returns(new List<DesktopUsageEntry>
            {
                CreateEntry(new DateTime(2026, 8, 19, 9, 0, 0), new DateTime(2026, 8, 19, 11, 0, 0)),
                CreateEntry(new DateTime(2026, 8, 20, 9, 0, 0), new DateTime(2026, 8, 20, 10, 0, 0))
            });
            var service = new UsageAnalysisService(usageTracker.Object);

            WorkingHoursEstimation estimation = service.EstimateWorkingHours(new DateTime(2026, 8, 19));

            estimation.Date.Should().Be(new DateTime(2026, 8, 19));
            estimation.TotalWorkedHours.Should().BeApproximately(2, 0.001);
        }

        [Fact]
        public async Task GenerateReportAsync_ReturnsEmptyResult_WhenNoDataExistsForDate()
        {
            var usageTracker = new Mock<IDesktopUsageTracker>();
            usageTracker.Setup(tracker => tracker.GetAllUsageHistory()).Returns(new List<DesktopUsageEntry>
            {
                CreateEntry(new DateTime(2026, 8, 19, 9, 0, 0), new DateTime(2026, 8, 19, 10, 0, 0))
            });
            var service = new UsageAnalysisService(usageTracker.Object);

            UsageReportResult result = await service.GenerateReportAsync(new UsageReportOptions(new DateTime(2026, 8, 20)));

            result.HasData.Should().BeFalse();
            result.Date.Should().Be(new DateTime(2026, 8, 20));
            result.TextReportPath.Should().BeEmpty();
        }

        [Fact]
        public async Task GenerateReportAsync_RejectsInvalidDurations()
        {
            var usageTracker = new Mock<IDesktopUsageTracker>();
            var service = new UsageAnalysisService(usageTracker.Object);

            Func<Task> action = () => service.GenerateReportAsync(new UsageReportOptions(DateTime.Today, MinimumDurationMinutes: -1));

            await action.Should().ThrowAsync<ArgumentOutOfRangeException>();
        }

        private static DesktopUsageEntry CreateEntry(DateTime start, DateTime end) => new()
        {
            DesktopName = "Development",
            StartTime = start,
            EndTime = end
        };
    }
}
