using FluentAssertions;
using System.IO;
using VirtualDesktopHelper.Configuration;
using VirtualDesktopHelper.Models;
using VirtualDesktopHelper.Services;
using Xunit;

namespace VirtualDesktopHelper.Tests.Integration
{
    /// <summary>
    /// Integration tests that verify the interaction between multiple components.
    /// </summary>
    public class DesktopUsageIntegrationTests : IDisposable
    {
        private readonly string _testDirectory;
        private readonly TrackerConfiguration _testConfig;

        public DesktopUsageIntegrationTests()
        {
            // Create a test-specific directory
            _testDirectory = Path.Combine(Path.GetTempPath(), "VirtualDesktopIntegrationTests", Guid.NewGuid().ToString());
            _testConfig = new TrackerConfiguration
            {
                LogDirectoryName = _testDirectory,
                LogFileNamePattern = "integration_test_{0:yyyy-MM-dd}.json"
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
        public void FullWorkflow_ShouldTrackUsageAndDetectProjects()
        {
            // Arrange
            var tracker = new DesktopUsageTracker(_testConfig);
            var projectDetectionService = new ProjectDetectionService();

            // Act - Simulate a user session
            tracker.TrackDesktopUsage("simon workspace"); // Should map to Selene project
            System.Threading.Thread.Sleep(50);
            
            tracker.TrackDesktopUsage("docker development"); // Should map to AI/ML project
            System.Threading.Thread.Sleep(50);
            
            tracker.TrackDesktopUsage("random desktop"); // Should map to default project
            System.Threading.Thread.Sleep(50);
            
            tracker.TrackDesktopUsage("archief management"); // Should map to archiving project

            // Get the tracked usage
            var usageLog = tracker.GetCurrentSessionUsageLog();
            
            // Detect projects for each entry
            var projectMappings = projectDetectionService.DetectProjectsForEntries(usageLog);

            // Assert
            usageLog.Should().HaveCount(4);

            // Verify desktop names are tracked correctly
            usageLog.Should().Contain(e => e.DesktopName == "simon workspace");
            usageLog.Should().Contain(e => e.DesktopName == "docker development");
            usageLog.Should().Contain(e => e.DesktopName == "random desktop");
            usageLog.Should().Contain(e => e.DesktopName == "archief management");

            // Verify project detection works correctly
            var simonEntry = usageLog.First(e => e.DesktopName == "simon workspace");
            projectMappings[simonEntry].Name.Should().Be("DWH - Technisch Onderhoud Selene");

            var dockerEntry = usageLog.First(e => e.DesktopName == "docker development");
            projectMappings[dockerEntry].Name.Should().Be("DWH - AI/ML Technische Verbeteringen");

            var randomEntry = usageLog.First(e => e.DesktopName == "random desktop");
            projectMappings[randomEntry].Name.Should().Be("Afwezig");

            var archiefEntry = usageLog.First(e => e.DesktopName == "archief management");
            projectMappings[archiefEntry].Name.Should().Be("DWH - Data Anonimisatie en Archivering");

            // Verify timing works correctly
            var completedEntries = usageLog.Where(e => !e.IsActive).ToList();
            completedEntries.Should().HaveCount(3); // Last entry should still be active
            
            foreach (var entry in completedEntries)
            {
                entry.Duration.Should().BeGreaterThan(TimeSpan.Zero);
                entry.EndTime.Should().NotBeNull();
            }

            // Verify last entry is still active
            var lastEntry = usageLog.Last();
            lastEntry.IsActive.Should().BeTrue();
            lastEntry.EndTime.Should().BeNull();
        }

        [Fact]
        public void ProjectDetection_ShouldHandleComplexDesktopNames()
        {
            // Arrange
            var projectDetectionService = new ProjectDetectionService();
            var testCases = new[]
            {
                new { DesktopName = "Simon's Development Environment", ExpectedProject = "DWH - Technisch Onderhoud Selene" },
                new { DesktopName = "DOCKER CONTAINER SETUP", ExpectedProject = "DWH - AI/ML Technische Verbeteringen" },
                new { DesktopName = "Data Archief Processing", ExpectedProject = "DWH - Data Anonimisatie en Archivering" },
                new { DesktopName = "Meeting Room", ExpectedProject = "Afwezig" },
                new { DesktopName = "Email and Teams", ExpectedProject = "Afwezig" }
            };

            foreach (var testCase in testCases)
            {
                // Act
                var entry = new DesktopUsageEntry { DesktopName = testCase.DesktopName };
                var detectedProject = projectDetectionService.DetectProjectForEntry(entry);

                // Assert
                detectedProject.Name.Should().Be(testCase.ExpectedProject, 
                    $"Desktop '{testCase.DesktopName}' should map to '{testCase.ExpectedProject}'");
            }
        }

        [Fact]
        public void UsageTracking_ShouldPersistToFileSystem()
        {
            // Arrange
            var tracker = new DesktopUsageTracker(_testConfig);

            // Act
            tracker.TrackDesktopUsage("Test Desktop");
            
            // Verify log directory was created
            Directory.Exists(tracker.GetLogDirectory()).Should().BeTrue();
            
            // Verify we have a log file path
            var logFilePath = tracker.GetCurrentLogFilePath();
            logFilePath.Should().NotBeNullOrEmpty();
            logFilePath.Should().StartWith(tracker.GetLogDirectory());
        }

        [Fact]
        public async Task ProjectConfiguration_ShouldBeThreadSafe()
        {
            // Arrange
            var tasks = new List<Task>();
            var results = new List<ProjectInfo>();
            var lockObject = new object();

            // Act - Simulate multiple threads accessing project configuration
            for (int i = 0; i < 10; i++)
            {
                var taskDesktopName = $"simon test {i}";
                tasks.Add(Task.Run(() =>
                {
                    var service = new ProjectDetectionService();
                    var entry = new DesktopUsageEntry { DesktopName = taskDesktopName };
                    var result = service.DetectProjectForEntry(entry);
                    
                    lock (lockObject)
                    {
                        results.Add(result);
                    }
                }));
            }

            await Task.WhenAll(tasks);

            // Assert
            results.Should().HaveCount(10);
            results.Should().OnlyContain(r => r.Name == "DWH - Technisch Onderhoud Selene");
        }
    }
}
