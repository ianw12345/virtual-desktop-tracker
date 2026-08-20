using Moq;
using VirtualDesktopHelper.Interfaces;
using VirtualDesktopHelper.Services;
using Xunit;

namespace VirtualDesktopHelper.Tests.Services
{
    public class DesktopTrackingCoordinatorTests
    {
        [Fact]
        public void Poll_TracksOnlyWhenDesktopChanges()
        {
            var desktopNames = new Mock<IWindowsDesktopNameService>();
            var screenState = new Mock<IScreenStateDetector>();
            var usageTracker = new Mock<IDesktopUsageTracker>();
            desktopNames.Setup(service => service.GetCurrentDesktopName()).Returns("Development");

            var coordinator = new DesktopTrackingCoordinator(
                desktopNames.Object,
                screenState.Object,
                usageTracker.Object);

            var firstUpdate = coordinator.Poll();
            var secondUpdate = coordinator.Poll();

            Assert.True(firstUpdate.HasChanged);
            Assert.False(secondUpdate.HasChanged);
            usageTracker.Verify(tracker => tracker.TrackDesktopUsage("Development"), Times.Once);
        }

        [Fact]
        public void Poll_UsesScreenOffWithoutReadingTheDesktopName()
        {
            var desktopNames = new Mock<IWindowsDesktopNameService>();
            var screenState = new Mock<IScreenStateDetector>();
            var usageTracker = new Mock<IDesktopUsageTracker>();
            screenState.Setup(detector => detector.IsScreenLockedOrOff()).Returns(true);

            var coordinator = new DesktopTrackingCoordinator(
                desktopNames.Object,
                screenState.Object,
                usageTracker.Object);

            var update = coordinator.Poll();

            Assert.Equal("Screen Off", update.DesktopName);
            usageTracker.Verify(tracker => tracker.TrackDesktopUsage("Screen Off"), Times.Once);
            desktopNames.Verify(service => service.GetCurrentDesktopName(), Times.Never);
        }
    }
}
