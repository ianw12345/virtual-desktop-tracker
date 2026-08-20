using FluentAssertions;
using Moq;
using System;
using System.Collections.Generic;
using System.IO;
using VirtualDesktopHelper.Configuration;
using VirtualDesktopHelper.Interfaces;
using VirtualDesktopHelper.Models;
using VirtualDesktopHelper.Services;
using Xunit;

namespace VirtualDesktopHelper.Tests.Services
{
    public class DesktopUsageTrackerTests : IDisposable
    {
        private readonly Mock<IWindowsDesktopNameService> _mockDesktopNameService;
        private readonly Mock<IVirtualDesktopErrorHandler> _mockErrorHandler;
        private readonly TrackerConfiguration _testConfig;
        private readonly string _testDirectory;

        public DesktopUsageTrackerTests()
        {
            _mockDesktopNameService = new Mock<IWindowsDesktopNameService>();
            _mockErrorHandler = new Mock<IVirtualDesktopErrorHandler>();
            
            // Create a test-specific configuration
            _testDirectory = Path.Combine(Path.GetTempPath(), "VirtualDesktopUsageTests", Guid.NewGuid().ToString());
            _testConfig = new TrackerConfiguration
            {
                LogDirectoryName = _testDirectory,
                LogFileNamePattern = "usage_log_{0:yyyy-MM-dd}.json"
            };

            // Clean up any existing test files
            if (Directory.Exists(_testDirectory))
            {
                Directory.Delete(_testDirectory, true);
            }
        }

        public void Dispose()
        {
            // Clean up test files
            if (Directory.Exists(_testDirectory))
            {
                Directory.Delete(_testDirectory, true);
            }
        }

        [Fact]
        public void Constructor_ShouldCreateLogDirectory()
        {
            // Act
            var tracker = new DesktopUsageTracker(_testConfig);

            // Assert
            Directory.Exists(tracker.GetLogDirectory()).Should().BeTrue();
        }

        [Fact]
        public void GetCurrentSessionUsageLog_ShouldReturnEmptyList_WhenNoUsageTracked()
        {
            // Arrange
            var tracker = new DesktopUsageTracker(_testConfig);

            // Act
            var result = tracker.GetCurrentSessionUsageLog();

            // Assert
            result.Should().NotBeNull();
            result.Should().BeEmpty();
        }

        [Fact]
        public void GetLogDirectory_ShouldReturnCorrectPath()
        {
            // Arrange
            var tracker = new DesktopUsageTracker(_testConfig);

            // Act
            var result = tracker.GetLogDirectory();

            // Assert
            result.Should().EndWith(_testDirectory);
        }

        [Fact]
        public void GetCurrentLogFilePath_ShouldReturnPathWithTodaysDate()
        {
            // Arrange
            var tracker = new DesktopUsageTracker(_testConfig);
            var expectedFileName = $"usage_log_{DateTime.Now:yyyy-MM-dd}.json";

            // Act
            var result = tracker.GetCurrentLogFilePath();

            // Assert
            result.Should().EndWith(expectedFileName);
            result.Should().StartWith(tracker.GetLogDirectory());
        }

        [Fact]
        public void TrackDesktopUsage_ShouldAddEntryToCurrentSession()
        {
            // Arrange
            var tracker = new DesktopUsageTracker(_testConfig);

            // Act
            tracker.TrackDesktopUsage("TestDesktop");

            // Assert
            var sessionLog = tracker.GetCurrentSessionUsageLog();
            sessionLog.Should().HaveCount(1);
            sessionLog[0].DesktopName.Should().Be("TestDesktop");
            sessionLog[0].IsActive.Should().BeTrue();
        }

        [Fact]
        public void TrackDesktopUsage_ShouldCloseActiveSessions_WhenSwitchingDesktops()
        {
            // Arrange
            var tracker = new DesktopUsageTracker(_testConfig);

            // Act
            tracker.TrackDesktopUsage("Desktop1");
            System.Threading.Thread.Sleep(10); // Small delay to ensure different timestamps
            tracker.TrackDesktopUsage("Desktop2");

            // Assert
            var sessionLog = tracker.GetCurrentSessionUsageLog();
            sessionLog.Should().HaveCount(2);
            
            // First desktop should be closed
            sessionLog[0].DesktopName.Should().Be("Desktop1");
            sessionLog[0].IsActive.Should().BeFalse();
            sessionLog[0].EndTime.Should().NotBeNull();
            
            // Second desktop should be active
            sessionLog[1].DesktopName.Should().Be("Desktop2");
            sessionLog[1].IsActive.Should().BeTrue();
        }

        [Fact]
        public void TrackDesktopUsage_ShouldNotCreateDuplicateEntries_WhenSameDesktopUsedConsecutively()
        {
            // Arrange
            var tracker = new DesktopUsageTracker(_testConfig);

            // Act
            tracker.TrackDesktopUsage("SameDesktop");
            tracker.TrackDesktopUsage("SameDesktop");
            tracker.TrackDesktopUsage("SameDesktop");

            // Assert
            var sessionLog = tracker.GetCurrentSessionUsageLog();
            sessionLog.Should().HaveCount(1);
            sessionLog[0].DesktopName.Should().Be("SameDesktop");
            sessionLog[0].IsActive.Should().BeTrue();
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void TrackDesktopUsage_ShouldHandleEmptyOrNullDesktopNames(string desktopName)
        {
            // Arrange
            var tracker = new DesktopUsageTracker(_testConfig);

            // Act
            tracker.TrackDesktopUsage(desktopName);

            // Assert
            // Should not create any entries for invalid desktop names
            var sessionLog = tracker.GetCurrentSessionUsageLog();
            sessionLog.Should().HaveCount(0);
        }

        [Fact]
        public void TrackDesktopUsage_ShouldCalculateCorrectDuration()
        {
            // Arrange
            var tracker = new DesktopUsageTracker(_testConfig);

            // Act
            tracker.TrackDesktopUsage("Desktop1");
            System.Threading.Thread.Sleep(100); // Wait a bit
            tracker.TrackDesktopUsage("Desktop2");

            // Assert
            var sessionLog = tracker.GetCurrentSessionUsageLog();
            sessionLog[0].Duration.Should().BeGreaterThan(TimeSpan.FromMilliseconds(50));
            sessionLog[0].Duration.Should().BeLessThan(TimeSpan.FromSeconds(1));
        }

        [Fact]
        public void StopTracking_ShouldCloseAndPersistTheActiveSession()
        {
            // Arrange
            var tracker = new DesktopUsageTracker(_testConfig);
            tracker.TrackDesktopUsage("Desktop1");

            // Act
            tracker.StopTracking();

            // Assert
            var sessionLog = tracker.GetCurrentSessionUsageLog();
            sessionLog.Should().ContainSingle();
            sessionLog[0].IsActive.Should().BeFalse();

            var persistedEntries = System.Text.Json.JsonSerializer.Deserialize<List<DesktopUsageEntry>>(
                File.ReadAllText(tracker.GetCurrentLogFilePath()));
            persistedEntries.Should().ContainSingle();
            persistedEntries![0].EndTime.Should().NotBeNull();
        }

        [Fact]
        public void GetAllUsageHistory_ShouldReturnEmptyList_WhenNoLogFilesExist()
        {
            // Arrange
            var tracker = new DesktopUsageTracker(_testConfig);

            // Act
            var result = tracker.GetAllUsageHistory();

            // Assert
            result.Should().NotBeNull();
            result.Should().BeEmpty();
        }

        [Fact]
        public void MultipleSessions_ShouldMaintainSeparateEntries()
        {
            // Arrange
            var tracker = new DesktopUsageTracker(_testConfig);

            // Act
            tracker.TrackDesktopUsage("Session1_Desktop1");
            tracker.TrackDesktopUsage("Session1_Desktop2");
            
            var sessionLog1 = tracker.GetCurrentSessionUsageLog();

            // Simulate new session by creating new tracker
            var tracker2 = new DesktopUsageTracker(_testConfig);
            tracker2.TrackDesktopUsage("Session2_Desktop1");
            
            var sessionLog2 = tracker2.GetCurrentSessionUsageLog();

            // Assert
            sessionLog1.Should().HaveCount(2);
            sessionLog2.Should().HaveCount(1);
            
            sessionLog1.Should().NotContain(e => e.DesktopName.StartsWith("Session2_"));
            sessionLog2.Should().NotContain(e => e.DesktopName.StartsWith("Session1_"));
        }
    }
}
