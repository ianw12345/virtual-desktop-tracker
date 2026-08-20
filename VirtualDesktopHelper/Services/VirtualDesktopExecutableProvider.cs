using System;
using System.IO;
using System.Reflection;

namespace VirtualDesktopHelper.Services
{
    /// <summary>
    /// Provides the 24H2 VirtualDesktop helper bundled with the application.
    /// A native executable must exist on disk before Windows can launch it, so the embedded helper
    /// is extracted to a private per-user cache on first use. The published application remains one file.
    /// </summary>
    public static class VirtualDesktopExecutableProvider
    {
        public const string ExecutableName = "VirtualDesktop11-24H2.exe";
        private const string EmbeddedResourceName = "VirtualDesktopHelper.Resources.VirtualDesktop11-24H2.exe";
        private static readonly object SyncRoot = new();
        private static string? _cachedPath;

        public static string GetPath()
        {
            lock (SyncRoot)
            {
                if (!string.IsNullOrEmpty(_cachedPath) && File.Exists(_cachedPath))
                {
                    return _cachedPath;
                }

                string cacheDirectory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "VirtualDesktopTracker",
                    "Helpers",
                    typeof(VirtualDesktopExecutableProvider).Assembly.GetName().Version?.ToString() ?? "current");
                string executablePath = Path.Combine(cacheDirectory, ExecutableName);

                Directory.CreateDirectory(cacheDirectory);
                if (!File.Exists(executablePath))
                {
                    using Stream source = GetEmbeddedHelperStream();
                    string temporaryPath = executablePath + ".tmp";
                    try
                    {
                        using (var destination = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None))
                        {
                            source.CopyTo(destination);
                        }

                        File.Move(temporaryPath, executablePath, overwrite: true);
                    }
                    finally
                    {
                        if (File.Exists(temporaryPath))
                        {
                            File.Delete(temporaryPath);
                        }
                    }
                }

                _cachedPath = executablePath;
                return executablePath;
            }
        }

        private static Stream GetEmbeddedHelperStream()
        {
            Stream? resource = typeof(VirtualDesktopExecutableProvider).Assembly
                .GetManifestResourceStream(EmbeddedResourceName);
            return resource ?? throw new FileNotFoundException(
                $"The embedded desktop helper '{EmbeddedResourceName}' was not found.");
        }
    }
}
