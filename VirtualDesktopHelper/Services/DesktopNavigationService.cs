using System;
using System.Collections.Generic;
using VirtualDesktopHelper.Interfaces;

namespace VirtualDesktopHelper.Services
{
    /// <summary>
    /// Switches to the desktop immediately before or after the currently visible desktop.
    /// The service is independent from input devices so it can be reused by mouse buttons,
    /// keyboard shortcuts, or future UI controls.
    /// </summary>
    public sealed class DesktopNavigationService
    {
        private readonly IWindowsDesktopNameService _desktopNameService;

        public DesktopNavigationService(IWindowsDesktopNameService desktopNameService)
        {
            _desktopNameService = desktopNameService ?? throw new ArgumentNullException(nameof(desktopNameService));
        }

        public DesktopNavigationResult Navigate(DesktopNavigationDirection direction)
        {
            List<string> desktopNames = _desktopNameService.GetAllDesktopNames();
            string currentDesktopName = _desktopNameService.GetCurrentDesktopName();
            int currentIndex = desktopNames.FindIndex(name => string.Equals(name, currentDesktopName, StringComparison.Ordinal));

            if (desktopNames.Count == 0 || currentIndex < 0)
            {
                return DesktopNavigationResult.Unavailable(currentDesktopName);
            }

            int targetIndex = currentIndex + (direction == DesktopNavigationDirection.Next ? 1 : -1);
            if (targetIndex < 0 || targetIndex >= desktopNames.Count)
            {
                return DesktopNavigationResult.AtBoundary(currentDesktopName);
            }

            string targetDesktopName = desktopNames[targetIndex];
            bool switched = _desktopNameService.SwitchToDesktop(targetDesktopName);
            return switched
                ? DesktopNavigationResult.Switched(currentDesktopName, targetDesktopName)
                : DesktopNavigationResult.Failed(currentDesktopName, targetDesktopName);
        }
    }

    public enum DesktopNavigationDirection
    {
        Previous,
        Next
    }

    public sealed record DesktopNavigationResult(
        string CurrentDesktopName,
        string? TargetDesktopName,
        bool WasSwitched,
        string? ErrorMessage)
    {
        public static DesktopNavigationResult Switched(string current, string target) => new(current, target, true, null);
        public static DesktopNavigationResult AtBoundary(string current) => new(current, null, false, null);
        public static DesktopNavigationResult Unavailable(string current) => new(current, null, false, "The current desktop could not be located.");
        public static DesktopNavigationResult Failed(string current, string target) => new(current, target, false, $"Could not switch to '{target}'.");
    }
}
