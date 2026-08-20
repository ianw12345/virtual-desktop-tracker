namespace VirtualDesktopDisplayer;

static class Program
{
    /// <summary>
    ///  The main entry point for the application.
    /// </summary>
    [STAThread]
    static void Main()
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 26100))
        {
            MessageBox.Show(
                "Virtual Desktop Tracker requires Windows 11 24H2 (build 26100) or later.\n\n" +
                "Your Windows build is not supported by the bundled VirtualDesktop helper.",
                "Unsupported Windows version",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return;
        }

        // To customize application configuration such as set high DPI settings or default font,
        // see https://aka.ms/applicationconfiguration.
        ApplicationConfiguration.Initialize();
        Application.Run(new VirtualDesktopDisplayForm());
    }    
}
