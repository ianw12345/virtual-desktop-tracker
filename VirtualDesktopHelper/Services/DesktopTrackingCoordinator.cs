using System;
using VirtualDesktopHelper.Configuration;
using VirtualDesktopHelper.Interfaces;

namespace VirtualDesktopHelper.Services
{
    /// <summary>
    /// Provides one shared polling and state-transition implementation for every host
    /// (the WinForms display and the command-line tracker).
    /// </summary>
    public sealed class DesktopTrackingCoordinator
    {
        private readonly IWindowsDesktopNameService _desktopNameService;
        private readonly IScreenStateDetector _screenStateDetector;
        private readonly IDesktopUsageTracker _usageTracker;
        private string _lastDesktopName = string.Empty;

        public DesktopTrackingCoordinator(
            IWindowsDesktopNameService desktopNameService,
            IScreenStateDetector screenStateDetector,
            IDesktopUsageTracker usageTracker)
        {
            _desktopNameService = desktopNameService ?? throw new ArgumentNullException(nameof(desktopNameService));
            _screenStateDetector = screenStateDetector ?? throw new ArgumentNullException(nameof(screenStateDetector));
            _usageTracker = usageTracker ?? throw new ArgumentNullException(nameof(usageTracker));
        }

        /// <summary>
        /// Reads the current desktop and records a new usage entry only when its name changed.
        /// Errors are returned to the host instead of being stored as desktop names.
        /// </summary>
        public DesktopTrackingUpdate Poll()
        {
            try
            {
                string currentDesktopName = _screenStateDetector.IsScreenLockedOrOff()
                    ? "Screen Off"
                    : _desktopNameService.GetCurrentDesktopName();

                if (string.IsNullOrWhiteSpace(currentDesktopName))
                {
                    return DesktopTrackingUpdate.Unknown;
                }

                bool hasChanged = !string.Equals(currentDesktopName, _lastDesktopName, StringComparison.Ordinal);
                string previousDesktopName = _lastDesktopName;

                if (hasChanged)
                {
                    _usageTracker.TrackDesktopUsage(currentDesktopName);
                    _lastDesktopName = currentDesktopName;
                }

                return new DesktopTrackingUpdate(currentDesktopName, previousDesktopName, hasChanged, null);
            }
            catch (Exception ex)
            {
                return new DesktopTrackingUpdate(_lastDesktopName, _lastDesktopName, false, ex.Message);
            }
        }

        /// <summary>
        /// Synchronizes the tracked state after a user-initiated rename, jump, or desktop creation.
        /// </summary>
        public void RecordDesktopName(string desktopName)
        {
            if (string.IsNullOrWhiteSpace(desktopName))
            {
                return;
            }

            if (!string.Equals(desktopName, _lastDesktopName, StringComparison.Ordinal))
            {
                _usageTracker.TrackDesktopUsage(desktopName);
                _lastDesktopName = desktopName;
            }
        }

        public void Stop() => _usageTracker.StopTracking();
    }

    public sealed record DesktopTrackingUpdate(
        string DesktopName,
        string PreviousDesktopName,
        bool HasChanged,
        string? ErrorMessage)
    {
        public static DesktopTrackingUpdate Unknown { get; } = new(string.Empty, string.Empty, false, null);
        public bool IsScreenOff => string.Equals(DesktopName, "Screen Off", StringComparison.Ordinal);
    }
}
