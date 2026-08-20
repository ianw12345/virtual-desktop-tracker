using System.Collections.Generic;
using FluentAssertions;
using Moq;
using VirtualDesktopHelper.Interfaces;
using VirtualDesktopHelper.Services;

namespace VirtualDesktopHelper.Tests.Services
{
    public class DesktopNavigationServiceTests
    {
        [Fact]
        public void Navigate_Next_SwitchesToFollowingDesktop()
        {
            var desktopNameService = CreateService(new[] { "Planning", "Development", "Review" }, "Development");
            var navigationService = new DesktopNavigationService(desktopNameService.Object);

            DesktopNavigationResult result = navigationService.Navigate(DesktopNavigationDirection.Next);

            result.WasSwitched.Should().BeTrue();
            result.TargetDesktopName.Should().Be("Review");
            desktopNameService.Verify(service => service.SwitchToDesktop("Review"), Times.Once);
        }

        [Fact]
        public void Navigate_Previous_DoesNotSwitchBeforeFirstDesktop()
        {
            var desktopNameService = CreateService(new[] { "Planning", "Development" }, "Planning");
            var navigationService = new DesktopNavigationService(desktopNameService.Object);

            DesktopNavigationResult result = navigationService.Navigate(DesktopNavigationDirection.Previous);

            result.WasSwitched.Should().BeFalse();
            result.TargetDesktopName.Should().BeNull();
            desktopNameService.Verify(service => service.SwitchToDesktop(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public void Navigate_ReturnsUnavailable_WhenCurrentDesktopIsMissingFromList()
        {
            var desktopNameService = CreateService(new[] { "Planning", "Development" }, "Unknown");
            var navigationService = new DesktopNavigationService(desktopNameService.Object);

            DesktopNavigationResult result = navigationService.Navigate(DesktopNavigationDirection.Next);

            result.WasSwitched.Should().BeFalse();
            result.ErrorMessage.Should().NotBeNullOrWhiteSpace();
        }

        private static Mock<IWindowsDesktopNameService> CreateService(IEnumerable<string> desktopNames, string currentDesktopName)
        {
            var service = new Mock<IWindowsDesktopNameService>();
            service.Setup(mock => mock.GetAllDesktopNames()).Returns(new List<string>(desktopNames));
            service.Setup(mock => mock.GetCurrentDesktopName()).Returns(currentDesktopName);
            service.Setup(mock => mock.SwitchToDesktop(It.IsAny<string>())).Returns(true);
            return service;
        }
    }
}
