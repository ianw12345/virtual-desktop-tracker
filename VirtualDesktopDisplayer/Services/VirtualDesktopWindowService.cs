using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using VirtualDesktopHelper.Configuration;

namespace VirtualDesktopDisplayer.Services
{
    /// <summary>
    /// Service for configuring windows to display across all virtual desktops.
    /// </summary>
    public class VirtualDesktopWindowService
    {
        private readonly TrackerConfiguration _config;

        // Windows API imports for window management
        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        // Constants for Windows API
        private const int GWL_EXSTYLE = -20;
        private const long WS_EX_TOOLWINDOW = 0x00000080L;
        private const long WS_EX_APPWINDOW = 0x00040000L;
        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_SHOWWINDOW = 0x0040;

        public VirtualDesktopWindowService(TrackerConfiguration? config = null)
        {
            _config = config ?? TrackerConfiguration.Instance;
        }

        /// <summary>
        /// Configures the specified window to appear on all virtual desktops.
        /// </summary>
        /// <param name="windowHandle">Handle to the window to configure.</param>
        /// <returns>True if configuration was successful, false otherwise.</returns>
        public bool ConfigureWindowForAllDesktops(IntPtr windowHandle)
        {
            if (windowHandle == IntPtr.Zero)
                return false;

            try
            {
                // Method 1: Try using subprocess to pin the window (safest approach)
                bool subprocessSuccess = TryPinWindowUsingSubprocess(windowHandle);

                // Method 2: Use standard Windows flags for topmost and visibility
                ConfigureWindowFlags(windowHandle);

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Warning: Could not configure window for all desktops: {ex.Message}");
                return false;
            }
        }

        private bool TryPinWindowUsingSubprocess(IntPtr windowHandle)
        {
            try
            {
                string virtualDesktopExe = FindVirtualDesktopExecutable();
                if (string.IsNullOrEmpty(virtualDesktopExe))
                    return false;

                // Pin this window to all desktops using its window handle.
                // /PinWindow expects a process name or ID; this.Handle is an HWND.
                using (var process = Process.Start(new ProcessStartInfo
                {
                    FileName = virtualDesktopExe,
                    Arguments = $"/PinWindowHandle:{windowHandle}",
                    WindowStyle = ProcessWindowStyle.Hidden,
                    CreateNoWindow = true,
                    UseShellExecute = false
                }))
                {
                    if (process != null && process.WaitForExit((int)_config.SubprocessTimeout.TotalMilliseconds))
                    {
                        return process.ExitCode == 0;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Subprocess pin window failed: {ex.Message}");
            }

            return false;
        }

        private void ConfigureWindowFlags(IntPtr windowHandle)
        {
            // Configure window extended style
            IntPtr currentStyle = GetWindowLongPtr(windowHandle, GWL_EXSTYLE);
            IntPtr newStyle = new IntPtr((currentStyle.ToInt64() & ~WS_EX_TOOLWINDOW) | WS_EX_APPWINDOW);
            SetWindowLongPtr(windowHandle, GWL_EXSTYLE, newStyle);

            // Ensure it stays on top
            SetWindowPos(windowHandle, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);
        }

        private string FindVirtualDesktopExecutable()
        {
            string startupPath = System.Windows.Forms.Application.StartupPath;
            string[] possiblePaths = {
                // The project copies these executables beside the application during the build.
                Path.Combine(startupPath, "VirtualDesktop11-24H2.exe"),
                Path.Combine(startupPath, "VirtualDesktop11.exe"),
                // Keep the source-tree locations as a fallback for development runs.
                Path.Combine(startupPath, "..", "..", "..", "VirtualDesktop", "VirtualDesktop11-24H2.exe"),
                Path.Combine(startupPath, "..", "..", "..", "VirtualDesktop", "VirtualDesktop11.exe")
            };

            foreach (string path in possiblePaths)
            {
                try
                {
                    string fullPath = Path.GetFullPath(path);
                    if (File.Exists(fullPath))
                        return fullPath;
                }
                catch
                {
                    // Skip invalid paths
                }
            }

            return "";
        }
    }
}
