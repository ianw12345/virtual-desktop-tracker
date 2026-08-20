using System.Collections.Generic;
using System.Threading.Tasks;
using VirtualDesktopHelper.Models;

namespace VirtualDesktopHelper.Interfaces
{
    /// <summary>
    /// Interface for tracking and persisting desktop usage data.
    /// </summary>
    public interface IDesktopUsageTracker
    {
        /// <summary>
        /// Records desktop usage when the user switches to a different desktop.
        /// </summary>
        /// <param name="desktopName">The name of the desktop being switched to.</param>
        void TrackDesktopUsage(string desktopName);

        /// <summary>
        /// Gets the current session's usage log.
        /// </summary>
        /// <returns>List of desktop usage entries for the current session.</returns>
        List<DesktopUsageEntry> GetCurrentSessionUsageLog();

        /// <summary>
        /// Gets usage data from all previous sessions.
        /// </summary>
        /// <returns>List of all desktop usage entries across all sessions.</returns>
        List<DesktopUsageEntry> GetAllUsageHistory();

        /// <summary>
        /// Gets the path to the current session's log file.
        /// </summary>
        /// <returns>Full path to the current log file.</returns>
        string GetCurrentLogFilePath();

        /// <summary>
        /// Gets the directory where log files are stored.
        /// </summary>
        /// <returns>Full path to the log directory.</returns>
        string GetLogDirectory();

        /// <summary>
        /// Generates a comprehensive usage report from all sessions.
        /// </summary>
        Task GenerateUsageReportAsync();

        /// <summary>
        /// Closes and persists the currently active desktop session.
        /// </summary>
        void StopTracking();

        /// <summary>
        /// Updates the desktop name for all entries with the specified old name from today only.
        /// This also renames the current desktop if it matches the old name.
        /// </summary>
        /// <param name="oldName">The current desktop name to update.</param>
        /// <param name="newName">The new desktop name to use.</param>
        /// <returns>True if the operation was successful (including desktop rename), false otherwise.</returns>
        bool UpdateDesktopNameForTodaysEntries(string oldName, string newName);
    }
}
