using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using VirtualDesktopDisplayer.Services;
using VirtualDesktopHelper;
using VirtualDesktopHelper.Configuration;
using VirtualDesktopHelper.Interfaces;
using VirtualDesktopHelper.Models;
using VirtualDesktopHelper.Services;

namespace VirtualDesktopDisplayer
{
    /// <summary>
    /// Main form for displaying virtual desktop information and handling desktop operations.
    /// </summary>
    public partial class VirtualDesktopDisplayForm : Form
    {
        // Services
        private readonly IWindowsDesktopNameService _desktopNameService;
        private readonly IDesktopUsageTracker _usageTracker;
        private readonly IScreenStateDetector _screenStateDetector;
        private readonly WindowPositionService _windowPositionService;
        private readonly VirtualDesktopWindowService _virtualDesktopService;
        private readonly ApplicationService _applicationService;
        private readonly TrackerConfiguration _config;
        private readonly DesktopTrackingCoordinator _trackingCoordinator;
        private readonly UsageAnalysisService _usageAnalysisService;
        private readonly DesktopNavigationService _desktopNavigationService;
        private readonly GlobalMouseNavigationService _globalMouseNavigationService;

        // UI Components
        private System.Windows.Forms.Timer? updateTimer;
        private Label? desktopLabel;
        private TextBox? renameTextBox;

        // State
        private bool _isRenameMode = false;
        private DesktopTrackingUpdate _lastTrackingUpdate = DesktopTrackingUpdate.Unknown;
        private DateTime _lastTrackingUpdateAt = DateTime.MinValue;
        private int _mouseNavigationInProgress;
        private bool IsManualClockifySessionActive => _config.ClockifyCheckInStartedAt.HasValue;

        public VirtualDesktopDisplayForm(
            IWindowsDesktopNameService? desktopNameService = null,
            IDesktopUsageTracker? usageTracker = null,
            IScreenStateDetector? screenStateDetector = null,
            TrackerConfiguration? config = null)
        {
            // Use service provider as fallback for DI
            _desktopNameService = desktopNameService ?? VirtualDesktopServiceProvider.GetDesktopNameService();
            _usageTracker = usageTracker ?? VirtualDesktopServiceProvider.GetUsageTracker();
            _screenStateDetector = screenStateDetector ?? VirtualDesktopServiceProvider.GetScreenStateDetector();
            _config = config ?? TrackerConfiguration.Instance;
            _trackingCoordinator = new DesktopTrackingCoordinator(_desktopNameService, _screenStateDetector, _usageTracker);
            _usageAnalysisService = new UsageAnalysisService(_usageTracker, _config);
            _desktopNavigationService = new DesktopNavigationService(_desktopNameService);
            _globalMouseNavigationService = new GlobalMouseNavigationService();
            _globalMouseNavigationService.NavigationRequested += OnMouseDesktopNavigationRequested;

            // Initialize services
            _windowPositionService = new WindowPositionService(_config);
            _virtualDesktopService = new VirtualDesktopWindowService(_config);
            _applicationService = new ApplicationService();

            try
            {
                InitializeDesktopDisplay();
                SetupUpdateTimer();
                _windowPositionService.PositionWindowBottomRight(this);
            }
            catch (Exception ex)
            {
                _applicationService.ShowError($"Error initializing form: {ex.Message}\n\nStack trace: {ex.StackTrace}");
                throw;
            }
        }

        protected override void SetVisibleCore(bool value)
        {
            base.SetVisibleCore(value);
            if (value)
            {
                // Configure window for all virtual desktops when first shown
                _virtualDesktopService.ConfigureWindowForAllDesktops(this.Handle);
            }
        }

        private void InitializeDesktopDisplay()
        {
            ConfigureForm();
            CreateControls();
            SetupEventHandlers();
            UpdateDesktopName();
        }

        private void ConfigureForm()
        {
            this.FormBorderStyle = FormBorderStyle.None;
            this.ShowInTaskbar = false;
            this.TopMost = true;
            this.BackColor = Color.DarkBlue;
            this.WindowState = FormWindowState.Normal;
            this.StartPosition = FormStartPosition.Manual;
            this.Text = "Virtual Desktop Displayer";
        }

        private void CreateControls()
        {
            // Create and configure the label
            desktopLabel = new Label
            {
                AutoSize = true,
                ForeColor = Color.White,
                BackColor = Color.DarkBlue,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter,
                Padding = new Padding(8, 4, 8, 4),
                Visible = true
            };

            // Create and configure the text box for renaming
            renameTextBox = new TextBox
            {
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                BackColor = Color.LightBlue,
                ForeColor = Color.Black,
                BorderStyle = BorderStyle.FixedSingle,
                Visible = false,
                Width = 150,
                Height = 20
            };

            this.Controls.Add(desktopLabel);
            this.Controls.Add(renameTextBox);
        }

        private void SetupEventHandlers()
        {
            this.DoubleClick += OnFormDoubleClick;
            this.MouseClick += OnFormMouseClick;
            if (desktopLabel != null)
            {
                desktopLabel.DoubleClick += OnFormDoubleClick;
                desktopLabel.MouseClick += OnFormMouseClick;
            }
            if (renameTextBox != null)
            {
                renameTextBox.KeyDown += OnRenameTextBoxKeyDown;
                renameTextBox.LostFocus += OnRenameTextBoxLostFocus;
            }
        }

        private void SetupUpdateTimer()
        {
            updateTimer = new System.Windows.Forms.Timer
            {
                Interval = (int)_config.ActiveScreenUpdateInterval.TotalMilliseconds
            };
            updateTimer.Tick += OnUpdateTimerTick;
            updateTimer.Start();
        }

        private void OnUpdateTimerTick(object? sender, EventArgs e)
        {
            UpdateDesktopName();
        }

        private void UpdateDesktopName()
        {
            try
            {
                // Don't update if we're in rename mode
                if (_isRenameMode)
                    return;

                DesktopTrackingUpdate trackingUpdate;
                if (_config.EnableManualClockifyTracking && !IsManualClockifySessionActive)
                {
                    string currentDesktopName = _screenStateDetector.IsScreenLockedOrOff()
                        ? "Screen Off"
                        : _desktopNameService.GetCurrentDesktopName();
                    trackingUpdate = new DesktopTrackingUpdate(currentDesktopName, string.Empty, false, null);
                }
                else
                {
                    trackingUpdate = _trackingCoordinator.Poll();
                }
                _lastTrackingUpdate = trackingUpdate;
                _lastTrackingUpdateAt = DateTime.Now;
                if (!string.IsNullOrEmpty(trackingUpdate.ErrorMessage))
                {
                    throw new InvalidOperationException(trackingUpdate.ErrorMessage);
                }

                string currentDesktop = trackingUpdate.DesktopName;

                if (!string.IsNullOrEmpty(currentDesktop))
                {
                    UpdateDisplayWithDesktopName(currentDesktop);
                    UpdateTimerInterval(currentDesktop);
                }
                else
                {
                    UpdateDisplayWithUnknownDesktop();
                }

                ResizeAndRepositionWindow();
            }
            catch (Exception ex)
            {
                if (desktopLabel != null)
                    desktopLabel.Text = $"Error: {ex.Message}";
            }
        }

        private void UpdateDisplayWithDesktopName(string desktopName)
        {
            if (desktopLabel != null)
                desktopLabel.Text = desktopName;
            this.Text = desktopName;
        }

        private void UpdateDisplayWithUnknownDesktop()
        {
            if (desktopLabel != null)
                desktopLabel.Text = "Desktop: Unknown";
            this.Text = "Virtual Desktop Displayer - Unknown";
        }

        private void UpdateTimerInterval(string currentDesktop)
        {
            if (updateTimer == null) return;

            int newInterval = currentDesktop == "Screen Off" ? 
                (int)_config.InactiveScreenUpdateInterval.TotalMilliseconds : 
                (int)_config.ActiveScreenUpdateInterval.TotalMilliseconds;

            if (updateTimer.Interval != newInterval)
            {
                updateTimer.Interval = newInterval;
            }
        }

        private void ResizeAndRepositionWindow()
        {
            if (desktopLabel != null)
            {
                this.Size = _windowPositionService.GetOptimalFormSize(desktopLabel);
                _windowPositionService.PositionWindowBottomRight(this);
            }
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            _windowPositionService.PositionWindowBottomRight(this);
            _virtualDesktopService.ConfigureWindowForAllDesktops(this.Handle);
            ApplyMouseDesktopNavigation(_config.EnableMouseDesktopNavigation, showError: false);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            updateTimer?.Stop();
            updateTimer?.Dispose();
            _globalMouseNavigationService.Dispose();
            _trackingCoordinator.Stop();
            base.OnFormClosing(e);
        }

        private void OnFormDoubleClick(object? sender, EventArgs e)
        {
            _applicationService.ExitApplication();
        }

        private void OnFormMouseClick(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                // Check if Ctrl is pressed for ticket number copy functionality
                if (Control.ModifierKeys == Keys.Control)
                {
                    OnCopyTicketNumberToClipboard();
                    return;
                }
                
                ShowContextMenu(e.Location);
            }
            else if (e.Button == MouseButtons.Left && !_isRenameMode)
            {
                // Check if Ctrl is pressed for enhanced functionality
                if (Control.ModifierKeys == Keys.Control)
                {
                    // If current desktop is "Daily Scrum", open the timeline view
                    if (string.Equals(desktopLabel?.Text, "Daily Scrum", StringComparison.OrdinalIgnoreCase))
                    {
                        OnTimelineViewClick(sender, e);
                        return;
                    }

                    // First check if current desktop doesn't have a ticket number
                    if (!CurrentDesktopHasTicketNumber())
                    {
                        // Check if clipboard contains a ticket number
                        string? clipboardTicket = GetTicketNumberFromClipboard();
                        if (!string.IsNullOrEmpty(clipboardTicket))
                        {
                            // Automatically rename desktop with ticket number
                            OnAutoRenameWithTicketFromClipboard(clipboardTicket);
                            return;
                        }
                    }
                    
                    // Fallback to original "Open Current Issue" functionality
                    OnOpenCurrentIssueClick(sender, e);
                }
                else
                {
                    ShowRenameTextBox();
                }
            }
        }

        private void ShowContextMenu(Point location)
        {
            var contextMenu = new ContextMenuStrip();

            contextMenu.Items.Add("Rename + Update Past Entries", null, OnRenameAndUpdatePastEntriesClick);
            
            // Add placeholder submenu items that will be populated asynchronously
            var renameAndUpdateRecentItem = new ToolStripMenuItem("Rename + Update To Recent...");
            renameAndUpdateRecentItem.DropDownItems.Add("Loading...", null, null);
            contextMenu.Items.Add(renameAndUpdateRecentItem);
            
            var recentNamesItem = new ToolStripMenuItem("Rename to Recent...");
            recentNamesItem.DropDownItems.Add("Loading...", null, null);

            var jumpToDesktopItem = new ToolStripMenuItem("Jump to Desktop...");
            jumpToDesktopItem.DropDownItems.Add("Loading...", null, null);
            contextMenu.Items.Add(jumpToDesktopItem);
            
            contextMenu.Items.Add(new ToolStripSeparator());
            
            // Group extras options under a single 'Extras' menu
            var extrasMenu = new ToolStripMenuItem("Extras");
            extrasMenu.DropDownItems.Add("Working Hours...", null, OnWorkingHoursEstimationClick);
            extrasMenu.DropDownItems.Add("Tracking Status", null, OnShowTrackingStatusClick);
            extrasMenu.DropDownItems.Add(new ToolStripSeparator());
            extrasMenu.DropDownItems.Add("Open Current Issue", null, OnOpenCurrentIssueClick);
            extrasMenu.DropDownItems.Add("Create New Desktop", null, OnCreateNewDesktopClick);
            extrasMenu.DropDownItems.Add(recentNamesItem);
            extrasMenu.DropDownItems.Add(new ToolStripSeparator());
            extrasMenu.DropDownItems.Add("View Log JSON", null, OnViewUsageLogClick);
            extrasMenu.DropDownItems.Add("Open Log Folder", null, OnOpenLogFolderClick);
            extrasMenu.DropDownItems.Add(new ToolStripSeparator());
            extrasMenu.DropDownItems.Add("Generate Report", null, OnGenerateReportClick);
            extrasMenu.DropDownItems.Add("Copy Timely JavaScript", null, OnCopyJavaScriptClick);
            extrasMenu.DropDownItems.Add("Upload to Timely (from time...)", null, OnUploadToTimelyFromTimeClick);
            extrasMenu.DropDownItems.Add("Upload to Clockify (from time...)", null, OnUploadToClockifyFromTimeClick);
            extrasMenu.DropDownItems.Add(new ToolStripSeparator());
            if (_config.EnableManualClockifyTracking)
            {
                extrasMenu.DropDownItems.Add(IsManualClockifySessionActive ? "Check out & upload to Clockify" : "Check in to Clockify", null,
                    IsManualClockifySessionActive ? OnClockifyCheckOutClick : OnClockifyCheckInClick);
            }
            contextMenu.Items.Add(extrasMenu);
            
            // Group configure options under a single 'Configure' menu
            var configureMenu = new ToolStripMenuItem("Configure");
            configureMenu.DropDownItems.Add("Timely", null, OnConfigureTimelyClick);
            configureMenu.DropDownItems.Add("Clockify", null, OnConfigureClockifyClick);
            configureMenu.DropDownItems.Add("Projects", null, OnConfigureProjectsClick);
            var manualTrackingItem = new ToolStripMenuItem("Manual Clockify check-in/check-out")
            {
                Checked = _config.EnableManualClockifyTracking,
                CheckOnClick = true
            };
            manualTrackingItem.Click += OnManualClockifyTrackingSettingClick;
            configureMenu.DropDownItems.Add(manualTrackingItem);
            configureMenu.DropDownItems.Add("Issue Tracking", null, OnConfigureIssueTrackingClick);
            var mouseNavigationItem = new ToolStripMenuItem("Use mouse Back/Forward buttons to switch desktops")
            {
                Checked = _config.EnableMouseDesktopNavigation,
                CheckOnClick = true
            };
            mouseNavigationItem.Click += OnMouseDesktopNavigationSettingClick;
            configureMenu.DropDownItems.Add(mouseNavigationItem);
            contextMenu.Items.Add(configureMenu);
            
            contextMenu.Items.Add("Upload to Timely", null, OnUploadToTimelyClick);
            contextMenu.Items.Add("Upload to Clockify", null, OnUploadToClockifyClick);
            contextMenu.Items.Add("Timeline View", null, OnTimelineViewClick);
            contextMenu.Items.Add(new ToolStripSeparator());
            contextMenu.Items.Add("Exit", null, (s, args) => _applicationService.ExitApplication());

            // Show the context menu relative to this form control to ensure it appears on the same virtual desktop
            // Using the overload that takes a Control ensures the context menu inherits the desktop behavior
            contextMenu.Show(this, location);

            // Asynchronously load the submenu items
            _ = Task.Run(async () => await LoadSubmenuItemsAsync(renameAndUpdateRecentItem, recentNamesItem, jumpToDesktopItem));
        }

        /// <summary>
        /// Asynchronously loads the submenu items for recent names and jump to desktop menus.
        /// This prevents blocking the UI when showing the context menu.
        /// </summary>
        /// <param name="renameAndUpdateRecentItem">The rename and update recent names menu item to populate</param>
        /// <param name="recentNamesItem">The recent names menu item to populate</param>
        /// <param name="jumpToDesktopItem">The jump to desktop menu item to populate</param>
        private async Task LoadSubmenuItemsAsync(ToolStripMenuItem renameAndUpdateRecentItem, ToolStripMenuItem recentNamesItem, ToolStripMenuItem jumpToDesktopItem)
        {
            try
            {
                // Load recent names in background
                var recentNamesTask = Task.Run(() => GetTodaysUniqueDesktopNames());
                
                // Load available desktops in background  
                var availableDesktopsTask = Task.Run(() => GetAvailableDesktops());

                // Wait for both tasks to complete
                var recentNames = await recentNamesTask;
                var availableDesktops = await availableDesktopsTask;

                // Update UI on the main thread
                if (renameAndUpdateRecentItem.IsDisposed || recentNamesItem.IsDisposed || jumpToDesktopItem.IsDisposed)
                    return;

                this.Invoke((Action)(() =>
                {
                    // Update rename and update recent names menu
                    renameAndUpdateRecentItem.DropDownItems.Clear();
                    if (recentNames.Count > 0)
                    {
                        foreach (var name in recentNames)
                        {
                            var menuItem = new ToolStripMenuItem(name);
                            menuItem.Click += (sender, e) => OnRenameAndUpdateToRecentNameClick(name);
                            renameAndUpdateRecentItem.DropDownItems.Add(menuItem);
                        }
                    }
                    else
                    {
                        var noItemsMenuItem = new ToolStripMenuItem("No recent names") { Enabled = false };
                        renameAndUpdateRecentItem.DropDownItems.Add(noItemsMenuItem);
                    }

                    // Update recent names menu
                    recentNamesItem.DropDownItems.Clear();
                    if (recentNames.Count > 0)
                    {
                        foreach (var name in recentNames)
                        {
                            var menuItem = new ToolStripMenuItem(name);
                            menuItem.Click += (sender, e) => OnRenameToRecentNameClick(name);
                            recentNamesItem.DropDownItems.Add(menuItem);
                        }
                    }
                    else
                    {
                        var noItemsMenuItem = new ToolStripMenuItem("No recent names") { Enabled = false };
                        recentNamesItem.DropDownItems.Add(noItemsMenuItem);
                    }

                    // Update jump to desktop menu
                    jumpToDesktopItem.DropDownItems.Clear();
                    if (availableDesktops.Count > 0)
                    {
                        foreach (var desktopName in availableDesktops)
                        {
                            var menuItem = new ToolStripMenuItem(desktopName);
                            menuItem.Click += (sender, e) => OnJumpToDesktopClick(desktopName);
                            jumpToDesktopItem.DropDownItems.Add(menuItem);
                        }
                    }
                    else
                    {
                        var noItemsMenuItem = new ToolStripMenuItem("No other desktops") { Enabled = false };
                        jumpToDesktopItem.DropDownItems.Add(noItemsMenuItem);
                    }
                }));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading submenu items asynchronously: {ex.Message}");
                
                // Update UI to show error on main thread
                if (!renameAndUpdateRecentItem.IsDisposed && !recentNamesItem.IsDisposed && !jumpToDesktopItem.IsDisposed)
                {
                    this.Invoke((Action)(() =>
                    {
                        renameAndUpdateRecentItem.DropDownItems.Clear();
                        renameAndUpdateRecentItem.DropDownItems.Add(new ToolStripMenuItem("Error loading") { Enabled = false });

                        recentNamesItem.DropDownItems.Clear();
                        recentNamesItem.DropDownItems.Add(new ToolStripMenuItem("Error loading") { Enabled = false });
                        
                        jumpToDesktopItem.DropDownItems.Clear();
                        jumpToDesktopItem.DropDownItems.Add(new ToolStripMenuItem("Error loading") { Enabled = false });
                    }));
                }
            }
        }

        /// <summary>
        /// Gets the list of available desktops excluding the current one.
        /// </summary>
        /// <returns>List of available desktop names</returns>
        private List<string> GetAvailableDesktops()
        {
            try
            {
                var allDesktopNames = _desktopNameService.GetAllDesktopNames();
                var currentDesktopName = _desktopNameService.GetCurrentDesktopName();
                
                // Filter out the current desktop and any error states
                var availableDesktops = allDesktopNames
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .Where(name => name != currentDesktopName)
                    .Where(name => !name.StartsWith("Error:") && name != "Unknown Desktop" && name != "Screen Off")
                    .OrderBy(name => name)
                    .ToList();
                
                return availableDesktops;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting available desktops: {ex.Message}");
                return new List<string>();
            }
        }

        /// <summary>
        /// Creates a submenu item for renaming to previously used desktop names from today.
        /// Excludes "Desktop n" and "Screen Off" patterns as specified.
        /// </summary>
        /// <returns>ToolStripMenuItem if there are recent names, null otherwise</returns>
        private ToolStripMenuItem? AddRecentDesktopNamesSubmenu()
        {
            try
            {
                var recentNames = GetTodaysUniqueDesktopNames();
                
                if (recentNames.Count == 0)
                {
                    return null; // No recent names to display
                }

                var submenuItem = new ToolStripMenuItem("Rename to Recent...");
                
                foreach (var name in recentNames)
                {
                    var menuItem = new ToolStripMenuItem(name);
                    menuItem.Click += (sender, e) => OnRenameToRecentNameClick(name);
                    submenuItem.DropDownItems.Add(menuItem);
                }
                
                return submenuItem;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error creating recent names submenu: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Gets unique desktop names used today, excluding the current desktop and filtered patterns.
        /// </summary>
        /// <returns>List of unique desktop names from today</returns>
        private List<string> GetTodaysUniqueDesktopNames()
        {
            try
            {
                var today = DateTime.Today;
                var currentDesktopName = _desktopNameService.GetCurrentDesktopName();
                
                // Get all usage history
                var allEntries = _usageTracker.GetAllUsageHistory();
                
                // Filter to today's entries and get unique names
                var uniqueNames = allEntries
                    .Where(entry => entry.StartTime.Date == today)
                    .Select(entry => entry.DesktopName)
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .Where(name => name != currentDesktopName) // Exclude current desktop
                    .Where(name => !IsExcludedDesktopName(name)) // Exclude patterns
                    .Distinct()
                    .OrderBy(name => name)
                    .ToList();
                
                return uniqueNames;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting today's unique desktop names: {ex.Message}");
                return new List<string>();
            }
        }

        /// <summary>
        /// Checks if a desktop name should be excluded from the recent names list.
        /// Excludes "Desktop n" and "Screen Off" patterns as requested.
        /// </summary>
        /// <param name="desktopName">The desktop name to check</param>
        /// <returns>True if the name should be excluded, false otherwise</returns>
        private bool IsExcludedDesktopName(string desktopName)
        {
            if (string.IsNullOrWhiteSpace(desktopName))
                return true;

            // Exclude "Desktop n" patterns (e.g., "Desktop 1", "Desktop 2", etc.)
            if (System.Text.RegularExpressions.Regex.IsMatch(desktopName, @"^Desktop \d+$", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                return true;

            // Exclude "Screen Off" pattern
            if (string.Equals(desktopName, "Screen Off", StringComparison.OrdinalIgnoreCase))
                return true;

            return false;
        }

        /// <summary>
        /// Creates a submenu item for jumping to specific virtual desktops.
        /// Shows all available virtual desktops except the current one.
        /// </summary>
        /// <returns>ToolStripMenuItem if there are other desktops available, null otherwise</returns>
        private ToolStripMenuItem? AddJumpToDesktopSubmenu()
        {
            try
            {
                var allDesktopNames = _desktopNameService.GetAllDesktopNames();
                var currentDesktopName = _desktopNameService.GetCurrentDesktopName();
                
                // Filter out the current desktop and any error states
                var availableDesktops = allDesktopNames
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .Where(name => name != currentDesktopName)
                    .Where(name => !name.StartsWith("Error:") && name != "Unknown Desktop" && name != "Screen Off")
                    .OrderBy(name => name)
                    .ToList();
                
                if (availableDesktops.Count == 0)
                {
                    return null; // No other desktops available
                }

                var submenuItem = new ToolStripMenuItem("Jump to Desktop...");
                
                foreach (var desktopName in availableDesktops)
                {
                    var menuItem = new ToolStripMenuItem(desktopName);
                    menuItem.Click += (sender, e) => OnJumpToDesktopClick(desktopName);
                    submenuItem.DropDownItems.Add(menuItem);
                }
                
                return submenuItem;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error creating jump to desktop submenu: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Handles clicking on a recent desktop name to rename the current desktop.
        /// </summary>
        /// <param name="newName">The name to rename the current desktop to</param>
        private void OnRenameToRecentNameClick(string newName)
        {
            try
            {
                string currentDesktopName = _desktopNameService.GetCurrentDesktopName();
                
                if (string.IsNullOrWhiteSpace(currentDesktopName) || currentDesktopName.StartsWith("Error:") || currentDesktopName == "Unknown Desktop")
                {
                    _applicationService.ShowWarning("Cannot determine current desktop name. Please ensure the desktop has a valid name.");
                    return;
                }

                if (currentDesktopName == newName)
                {
                    _applicationService.ShowWarning($"The current desktop is already named \"{newName}\".");
                    return;
                }

                // Confirm the action
                var confirmResult = MessageBox.Show(
                    $"Rename the current desktop from \"{currentDesktopName}\" to \"{newName}\"?",
                    "Confirm Desktop Rename",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (confirmResult != DialogResult.Yes)
                {
                    return;
                }

                // Perform the rename operation
                bool success = _desktopNameService.RenameCurrentDesktop(newName);

                if (success)
                {
                    // Update the display immediately
                    if (desktopLabel != null)
                    {
                        desktopLabel.Text = newName;
                    }
                    _trackingCoordinator.RecordDesktopName(newName);
                    
                    ShowToastNotification($"Desktop renamed to \"{newName}\".");
                }
                else
                {
                    _applicationService.ShowError("Failed to rename desktop. Please try again.");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error renaming to recent name: {ex.Message}");
                _applicationService.ShowError($"An error occurred while renaming the desktop: {ex.Message}");
            }
        }

        /// <summary>
        /// Handles clicking on a recent desktop name to rename the current desktop
        /// and update all today's entries with the old name.
        /// </summary>
        /// <param name="newName">The name to rename the current desktop to</param>
        private void OnRenameAndUpdateToRecentNameClick(string newName)
        {
            try
            {
                string currentDesktopName = _desktopNameService.GetCurrentDesktopName();

                if (string.IsNullOrWhiteSpace(currentDesktopName) || currentDesktopName.StartsWith("Error:") || currentDesktopName == "Unknown Desktop")
                {
                    _applicationService.ShowWarning("Cannot determine current desktop name. Please ensure the desktop has a valid name.");
                    return;
                }

                if (currentDesktopName == newName)
                {
                    _applicationService.ShowWarning($"The current desktop is already named \"{newName}\".");
                    return;
                }

                // Confirm the action
                var confirmResult = MessageBox.Show(
                    $"This will:\n\n" +
                    $"1. Rename the current desktop from \"{currentDesktopName}\" to \"{newName}\"\n" +
                    $"2. Update ALL entries from TODAY with the name \"{currentDesktopName}\" to use \"{newName}\"\n\n" +
                    "This action will modify your usage history. Are you sure you want to continue?",
                    "Confirm Desktop Rename & Update Past Entries",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (confirmResult != DialogResult.Yes)
                {
                    return;
                }

                bool success = _usageTracker.UpdateDesktopNameForTodaysEntries(currentDesktopName, newName);

                if (success)
                {
                    if (desktopLabel != null)
                    {
                        desktopLabel.Text = newName;
                    }
                    _trackingCoordinator.RecordDesktopName(newName);

                    ShowToastNotification($"Desktop renamed to \"{newName}\" and today's entries updated.");
                }
                else
                {
                    _applicationService.ShowError("Failed to rename desktop and update past entries. Please try again.");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error renaming and updating to recent name: {ex.Message}");
                _applicationService.ShowError($"An error occurred while renaming and updating past entries: {ex.Message}");
            }
        }

        /// <summary>
        /// Handles clicking on a desktop name to jump to that virtual desktop.
        /// </summary>
        /// <param name="desktopName">The name of the desktop to switch to</param>
        private void OnJumpToDesktopClick(string desktopName)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(desktopName))
                {
                    _applicationService.ShowWarning("Invalid desktop name specified.");
                    return;
                }

                // Perform the desktop switch without confirmation
                bool success = _desktopNameService.SwitchToDesktop(desktopName);

                if (success)
                {
                    // Update the display immediately to reflect the switch
                    if (desktopLabel != null)
                    {
                        desktopLabel.Text = desktopName;
                    }
                    _trackingCoordinator.RecordDesktopName(desktopName);
                }
                else
                {
                    _applicationService.ShowError("Failed to switch to desktop. Please try again.");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error jumping to desktop: {ex.Message}");
                _applicationService.ShowError($"An error occurred while switching to the desktop: {ex.Message}");
            }
        }

        /// <summary>
        /// Handles clicking on "Create New Desktop" to create a new virtual desktop and switch to it.
        /// </summary>
        /// <param name="sender">The sender object</param>
        /// <param name="e">Event arguments</param>
        private void OnCreateNewDesktopClick(object? sender, EventArgs e)
        {
            try
            {
                // Create new desktop and switch to it
                bool success = _desktopNameService.CreateNewDesktop(switchToNew: true);

                if (success)
                {
                    // Get the current desktop name after switching to update the display
                    string newDesktopName = _desktopNameService.GetCurrentDesktopName();
                    
                    if (desktopLabel != null && !string.IsNullOrWhiteSpace(newDesktopName))
                    {
                        desktopLabel.Text = newDesktopName;
                    }
                    _trackingCoordinator.RecordDesktopName(newDesktopName);
                }
                else
                {
                    _applicationService.ShowError("Failed to create new desktop. Please try again.");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error creating new desktop: {ex.Message}");
                _applicationService.ShowError($"An error occurred while creating a new desktop: {ex.Message}");
            }
        }

        private void ShowRenameTextBox()
        {
            if (desktopLabel == null || renameTextBox == null) return;

            _isRenameMode = true;

            desktopLabel.Visible = false;
            renameTextBox.Visible = true;
            renameTextBox.Text = desktopLabel.Text;
            renameTextBox.SelectAll();
            renameTextBox.Location = desktopLabel.Location;

            this.Size = _windowPositionService.GetOptimalFormSizeForTextBox(renameTextBox);
            _windowPositionService.PositionWindowBottomRight(this);

            renameTextBox.Focus();
        }

        private void HideRenameTextBox()
        {
            if (desktopLabel == null || renameTextBox == null) return;

            _isRenameMode = false;

            renameTextBox.Visible = false;
            desktopLabel.Visible = true;

            ResizeAndRepositionWindow();
        }

        private void OnRenameTextBoxKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
                ApplyDesktopRename();
            }
            else if (e.KeyCode == Keys.Escape)
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
                HideRenameTextBox();
            }
        }

        private void OnRenameTextBoxLostFocus(object? sender, EventArgs e)
        {
            HideRenameTextBox();
        }

        private void ApplyDesktopRename()
        {
            if (renameTextBox == null || desktopLabel == null) return;

            string newName = renameTextBox.Text.Trim();
            if (!string.IsNullOrEmpty(newName))
            {
                bool success = _desktopNameService.RenameCurrentDesktop(newName);
                if (success)
                {
                    desktopLabel.Text = newName;
                    _trackingCoordinator.RecordDesktopName(newName);
                }
                else
                {
                    _applicationService.ShowWarning("Failed to rename desktop. Please try again.");
                }
            }
            HideRenameTextBox();
        }

        private void OnViewUsageLogClick(object? sender, EventArgs e)
        {
            try
            {
                string logPath = _usageTracker.GetCurrentLogFilePath();
                if (File.Exists(logPath))
                {
                    _applicationService.OpenFileInNotepad(logPath);
                }
                else
                {
                    _applicationService.ShowInformation("No usage log found yet. The log will be created as you use different virtual desktops.");
                }
            }
            catch (Exception ex)
            {
                _applicationService.ShowError($"Error opening usage log: {ex.Message}");
            }
        }

        private async void OnGenerateReportClick(object? sender, EventArgs e)
        {
            using var optionsForm = new UsageReportOptionsForm();
            if (optionsForm.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }

            try
            {
                Cursor = Cursors.WaitCursor;
                var reportResult = await _usageAnalysisService.GenerateReportAsync(optionsForm.Options);
                if (!reportResult.HasData)
                {
                    _applicationService.ShowInformation($"No usage data found for {optionsForm.Options.Date:yyyy-MM-dd}.");
                    return;
                }

                _applicationService.OpenFileInNotepad(reportResult.TextReportPath);
                _applicationService.ShowInformation(
                    $"Report created for {reportResult.Date:yyyy-MM-dd}.\n\n" +
                    $"{reportResult.EntryCount} activities processed.\n" +
                    $"Text report: {reportResult.TextReportPath}\n" +
                    $"JSON report: {reportResult.JsonReportPath}",
                    "Usage Report Generated");
            }
            catch (Exception ex)
            {
                _applicationService.ShowError($"Error generating report: {ex.Message}");
            }
            finally
            {
                Cursor = Cursors.Default;
            }
        }

        private void OnWorkingHoursEstimationClick(object? sender, EventArgs e)
        {
            using var estimationForm = new WorkingHoursEstimationForm(_usageAnalysisService);
            estimationForm.ShowDialog(this);
        }

        private void OnShowTrackingStatusClick(object? sender, EventArgs e)
        {
            string desktopName = string.IsNullOrWhiteSpace(_lastTrackingUpdate.DesktopName)
                ? "Unknown"
                : _lastTrackingUpdate.DesktopName;
            string lastError = string.IsNullOrWhiteSpace(_lastTrackingUpdate.ErrorMessage)
                ? "None"
                : _lastTrackingUpdate.ErrorMessage;

            _applicationService.ShowInformation(
                $"Tracking is active.\n\n" +
                $"Current desktop: {desktopName}\n" +
                $"Last update: {(_lastTrackingUpdateAt == DateTime.MinValue ? "Not yet available" : _lastTrackingUpdateAt.ToString("HH:mm:ss"))}\n" +
                $"Active interval: {_config.ActiveScreenUpdateInterval.TotalSeconds:0.#} seconds\n" +
                $"Screen-off interval: {_config.InactiveScreenUpdateInterval.TotalSeconds:0.#} seconds\n" +
                $"Log folder: {_usageTracker.GetLogDirectory()}\n" +
                $"Last error: {lastError}",
                "Tracking Status");
        }

        private void OnMouseDesktopNavigationSettingClick(object? sender, EventArgs e)
        {
            if (sender is not ToolStripMenuItem menuItem)
            {
                return;
            }

            ApplyMouseDesktopNavigation(menuItem.Checked, showError: true);
        }

        private void OnManualClockifyTrackingSettingClick(object? sender, EventArgs e)
        {
            if (sender is not ToolStripMenuItem menuItem)
            {
                return;
            }

            if (!menuItem.Checked && IsManualClockifySessionActive)
            {
                menuItem.Checked = true;
                _applicationService.ShowWarning("Check out of Clockify before disabling manual tracking.");
                return;
            }

            _config.EnableManualClockifyTracking = menuItem.Checked;
            if (menuItem.Checked)
            {
                _trackingCoordinator.Stop();
                ShowToastNotification("Manual Clockify tracking enabled. Check in when you start work.");
            }
            else
            {
                ShowToastNotification("Manual Clockify tracking disabled. Automatic tracking resumed.");
                UpdateDesktopName();
            }

            _config.SaveConfiguration();
        }

        private void OnClockifyCheckInClick(object? sender, EventArgs e)
        {
            try
            {
                if (!ClockifyConfiguration.Instance.IsConfigured())
                {
                    _applicationService.ShowWarning("Configure Clockify before checking in.");
                    ShowClockifyConfigurationDialog();
                    return;
                }

                _trackingCoordinator.Stop();
                _config.ClockifyCheckInStartedAt = DateTime.Now;
                _config.SaveConfiguration();
                UpdateDesktopName();
                ShowToastNotification($"Checked in to Clockify at {_config.ClockifyCheckInStartedAt:HH:mm}.");
            }
            catch (Exception ex)
            {
                _applicationService.ShowError($"Could not check in to Clockify: {ex.Message}");
            }
        }

        private async void OnClockifyCheckOutClick(object? sender, EventArgs e)
        {
            if (!IsManualClockifySessionActive)
            {
                return;
            }

            DateTime checkInStartedAt = _config.ClockifyCheckInStartedAt!.Value;
            try
            {
                UpdateDesktopName();
                _trackingCoordinator.Stop();
                _config.ClockifyCheckInStartedAt = null;
                _config.SaveConfiguration();

                List<DesktopUsageEntry> entries = _usageTracker.GetAllUsageHistory();
                using var service = new ClockifyApiService();
                ClockifyUploadResult result = await service.UploadAsync(entries, currentDayOnly: false, fromTime: checkInStartedAt);
                if (result.Success)
                {
                    ShowToastNotification($"Checked out. Uploaded {result.SuccessCount} Clockify entries.");
                }
                else
                {
                    string details = result.Errors.Any() ? "\n\n" + string.Join("\n", result.Errors.Take(10)) : string.Empty;
                    _applicationService.ShowError($"Checked out, but Clockify upload failed.{details}");
                }
            }
            catch (Exception ex)
            {
                _applicationService.ShowError($"Checked out, but Clockify upload failed: {ex.Message}");
            }
        }

        private void ApplyMouseDesktopNavigation(bool enabled, bool showError)
        {
            try
            {
                if (enabled)
                {
                    _globalMouseNavigationService.Start();
                }
                else
                {
                    _globalMouseNavigationService.Stop();
                }

                _config.EnableMouseDesktopNavigation = enabled;
                _config.SaveConfiguration();
            }
            catch (Exception ex)
            {
                _globalMouseNavigationService.Stop();
                _config.EnableMouseDesktopNavigation = false;
                _config.SaveConfiguration();

                if (showError)
                {
                    _applicationService.ShowError($"Mouse-button desktop navigation could not be enabled: {ex.Message}");
                }
            }
        }

        private void OnMouseDesktopNavigationRequested(DesktopNavigationDirection direction)
        {
            if (Interlocked.Exchange(ref _mouseNavigationInProgress, 1) != 0)
            {
                return;
            }

            _ = NavigateWithMouseAsync(direction);
        }

        private async Task NavigateWithMouseAsync(DesktopNavigationDirection direction)
        {
            try
            {
                DesktopNavigationResult result = await Task.Run(() => _desktopNavigationService.Navigate(direction));
                if (IsDisposed || !result.WasSwitched)
                {
                    return;
                }

                BeginInvoke((Action)(() =>
                {
                    UpdateDesktopName();
                    _applicationService.ShowToast(this, $"Switched to {result.TargetDesktopName}");
                }));
            }
            catch (Exception ex)
            {
                if (!IsDisposed && IsHandleCreated)
                {
                    BeginInvoke((Action)(() => _applicationService.ShowToast(this, $"Desktop switch failed: {ex.Message}")));
                }
            }
            finally
            {
                Interlocked.Exchange(ref _mouseNavigationInProgress, 0);
            }
        }

        private void OnTimelineViewClick(object? sender, EventArgs e)
        {
            try
            {
                var timelineForm = new TimelineViewForm(_usageTracker);
                timelineForm.Show();
            }
            catch (Exception ex)
            {
                _applicationService.ShowError($"Error opening timeline view: {ex.Message}");
            }
        }

        private void OnCopyJavaScriptClick(object? sender, EventArgs e)
        {
            try
            {
                // Check if Timely configuration is set up
                var timelyConfig = TimelyConfiguration.Instance;
                if (!timelyConfig.IsConfigured())
                {
                    var result = MessageBox.Show(
                        "Timely configuration is not set up. Would you like to configure it now?\n\n" +
                        "You'll need to provide:\n" +
                        "- CSRF Token (from browser network requests)\n" +
                        "- Cookie String (from browser)\n" +
                        "- Project ID and User ID (from Timely)\n\n" +
                        "The configuration file will be created at:\n" +
                        TimelyConfiguration.GetConfigFilePath(),
                        "Timely Configuration Required",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question);

                    if (result == DialogResult.Yes)
                    {
                        ShowTimelyConfigurationDialog();
                        return;
                    }
                    else
                    {
                        _applicationService.ShowInformation("Timely JavaScript generation cancelled.");
                        return;
                    }
                }

                // Generate the JavaScript
                var generator = new TimelyJavaScriptGenerator(_config, timelyConfig);
                var allEntries = _usageTracker.GetAllUsageHistory();
                
                if (!allEntries.Any())
                {
                    _applicationService.ShowInformation("No usage data available to generate Timely JavaScript.");
                    return;
                }

                var javascript = generator.GenerateTimelyJavaScript(allEntries, currentDayOnly: true);

                // Copy to clipboard
                Clipboard.SetText(javascript);

                ShowToastNotification("Timely JavaScript copied to clipboard.");
            }
            catch (Exception ex)
            {
                _applicationService.ShowError($"Error generating Timely JavaScript: {ex.Message}");
            }
        }

        private async void OnUploadToTimelyClick(object? sender, EventArgs e)
        {
            try
            {
                // Check if Timely configuration is set up
                var timelyConfig = TimelyConfiguration.Instance;
                if (!timelyConfig.IsConfigured())
                {
                    var result = MessageBox.Show(
                        "Timely configuration is not set up. Would you like to configure it now?\n\n" +
                        "You'll need to provide:\n" +
                        "- CSRF Token (from browser network requests)\n" +
                        "- Cookie String (from browser)\n" +
                        "- Project ID and User ID (from Timely)\n\n" +
                        "The configuration file will be created at:\n" +
                        TimelyConfiguration.GetConfigFilePath(),
                        "Timely Configuration Required",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question);

                    if (result == DialogResult.Yes)
                    {
                        ShowTimelyConfigurationDialog();
                        return;
                    }
                    else
                    {
                        _applicationService.ShowInformation("Timely upload cancelled.");
                        return;
                    }
                }

                // Get usage data
                var allEntries = _usageTracker.GetAllUsageHistory();
                
                if (!allEntries.Any())
                {
                    _applicationService.ShowInformation("No usage data available to upload to Timely.");
                    return;
                }

                // Confirm the upload action
                var confirmResult = MessageBox.Show(
                    "This will upload your usage data directly to Timely.\n\n" +
                    "Are you sure you want to proceed?\n\n" +
                    "Note: This will create time entries in your Timely workspace.",
                    "Confirm Timely Upload",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (confirmResult != DialogResult.Yes)
                {
                    return;
                }

                // Show progress message
                var progressForm = new Form()
                {
                    Text = "Uploading to Timely",
                    Size = new Size(300, 100),
                    StartPosition = FormStartPosition.CenterParent,
                    FormBorderStyle = FormBorderStyle.FixedDialog,
                    MaximizeBox = false,
                    MinimizeBox = false
                };

                var progressLabel = new Label()
                {
                    Text = "Uploading time entries to Timely...",
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleCenter
                };

                progressForm.Controls.Add(progressLabel);
                progressForm.Show();
                Application.DoEvents(); // Ensure the form is displayed

                try
                {
                    // Upload to Timely
                    using (var timelyService = new TimelyApiService())
                    {
                        var uploadResult = await timelyService.UploadToTimelyAsync(allEntries, currentDayOnly: true);

                        progressForm.Close();

                        if (uploadResult.Success)
                        {
                            ShowToastNotification($"Uploaded {uploadResult.SuccessCount} entries to Timely.");
                        }
                        else
                        {
                            var errorMessage = $"No entries were successfully uploaded ({uploadResult.FailureCount} failed)";
                            if (uploadResult.Errors.Any())
                            {
                                errorMessage += "\n\nErrors:\n" + string.Join("\n", uploadResult.Errors.Take(10));
                                if (uploadResult.Errors.Count > 10)
                                {
                                    errorMessage += $"\n... and {uploadResult.Errors.Count - 10} more errors";
                                }
                            }

                            _applicationService.ShowError(errorMessage);

                            // Add a button to reconfigure Timely on error
                            var reconfigureResult = MessageBox.Show(
                                "Would you like to reconfigure Timely settings to attempt to resolve the issue?",
                                "Reconfigure Timely",
                                MessageBoxButtons.YesNo,
                                MessageBoxIcon.Question);

                            if (reconfigureResult == DialogResult.Yes)
                            {
                                ShowTimelyConfigurationDialog();
                            }
                        }
                    }
                }
                catch (Exception)
                {
                    progressForm.Close();
                    throw; // Re-throw to be caught by outer catch block
                }
            }
            catch (Exception ex)
            {
                _applicationService.ShowError($"Error uploading to Timely: {ex.Message}");
            }
        }

        private async void OnUploadToTimelyFromTimeClick(object? sender, EventArgs e)
        {
            try
            {
                // Check if Timely configuration is set up
                var timelyConfig = TimelyConfiguration.Instance;
                if (!timelyConfig.IsConfigured())
                {
                    var result = MessageBox.Show(
                        "Timely configuration is not set up. Would you like to configure it now?\n\n" +
                        "You'll need to provide:\n" +
                        "- CSRF Token (from browser network requests)\n" +
                        "- Cookie String (from browser)\n" +
                        "- Project ID and User ID (from Timely)\n\n" +
                        "The configuration file will be created at:\n" +
                        TimelyConfiguration.GetConfigFilePath(),
                        "Timely Configuration Required",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question);

                    if (result == DialogResult.Yes)
                    {
                        ShowTimelyConfigurationDialog();
                        return;
                    }
                    else
                    {
                        _applicationService.ShowInformation("Timely upload cancelled.");
                        return;
                    }
                }

                // Show time selection dialog
                using (var timeSelectionForm = new TimelyTimeSelectionForm())
                {
                    if (timeSelectionForm.ShowDialog() != DialogResult.OK)
                    {
                        return; // User cancelled time selection
                    }

                    var fromTime = timeSelectionForm.SelectedTime;

                    // Get usage data
                    var allEntries = _usageTracker.GetAllUsageHistory();
                    
                    if (!allEntries.Any())
                    {
                        _applicationService.ShowInformation("No usage data available to upload to Timely.");
                        return;
                    }

                    // Confirm the upload action
                    var confirmResult = MessageBox.Show(
                        $"This will upload today's usage data from {fromTime:HH:mm} onwards to Timely.\n\n" +
                        "Only records from today that ended after the selected time will be uploaded.\n\n" +
                        "Are you sure you want to proceed?\n\n" +
                        "Note: This will create time entries in your Timely workspace.",
                        "Confirm Timely Upload",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question);

                    if (confirmResult != DialogResult.Yes)
                    {
                        return;
                    }

                    // Show progress message
                    var progressForm = new Form()
                    {
                        Text = "Uploading to Timely",
                        Size = new Size(320, 100),
                        StartPosition = FormStartPosition.CenterParent,
                        FormBorderStyle = FormBorderStyle.FixedDialog,
                        MaximizeBox = false,
                        MinimizeBox = false
                    };

                    var progressLabel = new Label()
                    {
                        Text = $"Uploading today's entries from {fromTime:HH:mm}...",
                        Dock = DockStyle.Fill,
                        TextAlign = ContentAlignment.MiddleCenter
                    };

                    progressForm.Controls.Add(progressLabel);
                    progressForm.Show();
                    Application.DoEvents(); // Ensure the form is displayed

                    try
                    {
                        // Upload to Timely with custom from time (but still current day only)
                        using (var timelyService = new TimelyApiService())
                        {
                            var uploadResult = await timelyService.UploadToTimelyAsync(allEntries, currentDayOnly: true, fromTime: fromTime);

                            progressForm.Close();

                            if (uploadResult.Success)
                            {
                                ShowToastNotification($"Uploaded {uploadResult.SuccessCount} entries from {fromTime:HH:mm} to Timely.");
                            }
                            else
                            {
                                var errorMessage = $"No entries were successfully uploaded ({uploadResult.FailureCount} failed)";
                                if (uploadResult.Errors.Any())
                                {
                                    errorMessage += "\n\nErrors:\n" + string.Join("\n", uploadResult.Errors.Take(10));
                                    if (uploadResult.Errors.Count > 10)
                                    {
                                        errorMessage += $"\n... and {uploadResult.Errors.Count - 10} more errors";
                                    }
                                }

                                _applicationService.ShowError(errorMessage);

                                // Add a button to reconfigure Timely on error
                                var reconfigureResult = MessageBox.Show(
                                    "Would you like to reconfigure Timely settings to attempt to resolve the issue?",
                                    "Reconfigure Timely",
                                    MessageBoxButtons.YesNo,
                                    MessageBoxIcon.Question);

                                if (reconfigureResult == DialogResult.Yes)
                                {
                                    ShowTimelyConfigurationDialog();
                                }
                            }
                        }
                    }
                    catch (Exception)
                    {
                        progressForm.Close();
                        throw; // Re-throw to be caught by outer catch block
                    }
                }
            }
            catch (Exception ex)
            {
                _applicationService.ShowError($"Error uploading to Timely: {ex.Message}");
            }
        }

        private async void OnUploadToClockifyClick(object? sender, EventArgs e)
        {
            await UploadToClockifyAsync(null);
        }

        private async void OnUploadToClockifyFromTimeClick(object? sender, EventArgs e)
        {
            using var timeSelectionForm = new TimelyTimeSelectionForm();
            if (timeSelectionForm.ShowDialog() == DialogResult.OK)
            {
                await UploadToClockifyAsync(timeSelectionForm.SelectedTime);
            }
        }

        private async Task UploadToClockifyAsync(DateTime? fromTime)
        {
            try
            {
                if (!ClockifyConfiguration.Instance.IsConfigured())
                {
                    var setup = MessageBox.Show(
                        "Clockify is not configured. An API key, workspace and default project are required. Configure it now?",
                        "Clockify configuration required",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question);
                    if (setup == DialogResult.Yes)
                    {
                        ShowClockifyConfigurationDialog();
                    }

                    return;
                }

                List<DesktopUsageEntry> allEntries = _usageTracker.GetAllUsageHistory();
                if (!allEntries.Any())
                {
                    _applicationService.ShowInformation("No usage data is available to upload to Clockify.");
                    return;
                }

                string period = fromTime.HasValue ? $"from {fromTime.Value:HH:mm}" : "for today";
                var confirmation = MessageBox.Show(
                    $"This creates Clockify time entries {period}. Continue?",
                    "Confirm Clockify upload",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);
                if (confirmation != DialogResult.Yes)
                {
                    return;
                }

                using var progressForm = new Form
                {
                    Text = "Uploading to Clockify",
                    Size = new Size(310, 100),
                    StartPosition = FormStartPosition.CenterParent,
                    FormBorderStyle = FormBorderStyle.FixedDialog,
                    MaximizeBox = false,
                    MinimizeBox = false
                };
                progressForm.Controls.Add(new Label
                {
                    Text = "Uploading time entries to Clockify…",
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleCenter
                });
                progressForm.Show(this);
                Application.DoEvents();

                ClockifyUploadResult uploadResult;
                try
                {
                    using var service = new ClockifyApiService();
                    uploadResult = await service.UploadAsync(allEntries, currentDayOnly: true, fromTime: fromTime);
                }
                finally
                {
                    progressForm.Close();
                }

                if (uploadResult.Success)
                {
                    ShowToastNotification($"Uploaded {uploadResult.SuccessCount} entries to Clockify.");
                    return;
                }

                string errors = uploadResult.Errors.Any()
                    ? "\n\nErrors:\n" + string.Join("\n", uploadResult.Errors.Take(10))
                    : string.Empty;
                _applicationService.ShowError($"No Clockify entries were uploaded ({uploadResult.FailureCount} failed).{errors}");
            }
            catch (Exception ex)
            {
                _applicationService.ShowError($"Error uploading to Clockify: {ex.Message}");
            }
        }

        private void ShowTimelyConfigurationDialog()
        {
            var configForm = new TimelyConfigurationFormEnhanced();
            if (configForm.ShowDialog() == DialogResult.OK)
            {
                ShowToastNotification("Timely configuration saved.");
            }
        }

        private void OnConfigureTimelyClick(object? sender, EventArgs e)
        {
            try
            {
                ShowTimelyConfigurationDialog();
            }
            catch (Exception ex)
            {
                _applicationService.ShowError($"Error opening Timely configuration: {ex.Message}");
            }
        }

        private void OnConfigureClockifyClick(object? sender, EventArgs e)
        {
            try
            {
                ShowClockifyConfigurationDialog();
            }
            catch (Exception ex)
            {
                _applicationService.ShowError($"Error opening Clockify configuration: {ex.Message}");
            }
        }

        private void ShowClockifyConfigurationDialog()
        {
            using var configurationForm = new ClockifyConfigurationForm();
            if (configurationForm.ShowDialog(this) == DialogResult.OK)
            {
                ShowToastNotification("Clockify configuration saved.");
            }
        }

        private void OnConfigureProjectsClick(object? sender, EventArgs e)
        {
            try
            {
                using (var form = new ProjectConfigurationForm())
                {
                    if (form.ShowDialog() == DialogResult.OK)
                    {
                        ShowToastNotification("Project configuration saved.");
                    }
                }
            }
            catch (Exception ex)
            {
                _applicationService.ShowError($"Error opening project configuration: {ex.Message}");
            }
        }

        private void OnOpenLogFolderClick(object? sender, EventArgs e)
        {
            try
            {
                string logDirectory = _usageTracker.GetLogDirectory();
                if (!_applicationService.OpenFolderInExplorer(logDirectory))
                {
                    // Fallback to Documents folder
                    string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                    _applicationService.OpenFolderInExplorer(documentsPath);
                }
            }
            catch (Exception ex)
            {
                _applicationService.ShowError($"Error opening folder: {ex.Message}");
            }
        }

        private void OnRenameAndUpdatePastEntriesClick(object? sender, EventArgs e)
        {
            try
            {
                string currentDesktopName = _desktopNameService.GetCurrentDesktopName();
                
                if (string.IsNullOrWhiteSpace(currentDesktopName) || currentDesktopName.StartsWith("Error:") || currentDesktopName == "Unknown Desktop")
                {
                    _applicationService.ShowWarning("Cannot determine current desktop name. Please ensure the desktop has a valid name.");
                    return;
                }

                // Show input dialog for new name
                string? newName = ShowInputDialog("Rename Desktop & Update Past Entries", 
                    $"Current desktop: {currentDesktopName}\n\nEnter new name:", 
                    currentDesktopName);

                if (string.IsNullOrWhiteSpace(newName) || newName == currentDesktopName)
                {
                    return; // User cancelled or entered same name
                }

                // Confirm the action
                var confirmResult = MessageBox.Show(
                    $"This will:\n\n" +
                    $"1. Rename the current desktop from \"{currentDesktopName}\" to \"{newName}\"\n" +
                    $"2. Update ALL entries from TODAY with the name \"{currentDesktopName}\" to use \"{newName}\"\n\n" +
                    $"This action will modify your usage history. Are you sure you want to continue?",
                    "Confirm Desktop Rename & Update Past Entries",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (confirmResult != DialogResult.Yes)
                {
                    return;
                }

                // Perform the rename and update operation
                bool success = _usageTracker.UpdateDesktopNameForTodaysEntries(currentDesktopName, newName);

                if (success)
                {
                    // Update the display immediately
                    if (desktopLabel != null)
                    {
                        desktopLabel.Text = newName;
                    }
                    _trackingCoordinator.RecordDesktopName(newName);

                    ShowToastNotification($"Desktop renamed to \"{newName}\" and today's entries updated.");
                }
                else
                {
                    _applicationService.ShowError(
                        "Failed to rename desktop or update past entries. This could be due to:\n\n" +
                        "• Virtual desktop rename operation failed\n" +
                        "• File permission issues with log files\n" +
                        "• Invalid desktop names\n\n" +
                        "Please try again or check the log files manually.");
                }
            }
            catch (Exception ex)
            {
                _applicationService.ShowError($"Error during rename and update operation: {ex.Message}");
            }
        }

        /// <summary>
        /// Shows a simple input dialog to get text input from the user.
        /// </summary>
        /// <param name="title">The dialog title.</param>
        /// <param name="prompt">The prompt message.</param>
        /// <param name="defaultValue">The default value to show in the input field.</param>
        /// <returns>The entered text, or null if cancelled.</returns>
        private string? ShowInputDialog(string title, string prompt, string defaultValue = "")
        {
            return _applicationService.ShowTextInputDialog(this, title, prompt, defaultValue);
        }

        private void OnOpenCurrentIssueClick(object? sender, EventArgs e)
        {
            try
            {
                var issueService = new IssueTrackingService(_config);
                
                if (!issueService.IsConfigured())
                {
                    var result = MessageBox.Show(
                        "Issue tracking is not configured. Would you like to configure it now?\n\n" +
                        "You'll need to provide:\n" +
                        "- Issue format (regex pattern)\n" +
                        "- Issue URL template\n\n" +
                        "Example patterns:\n" +
                        "- \\b[A-Z][A-Z0-9]+-\\d+\\b for JIRA-style (APP-5482)\n" +
                        "- #\\d+ for GitHub-style (#123)",
                        "Issue Tracking Configuration Required",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question);

                    if (result == DialogResult.Yes)
                    {
                        ShowIssueTrackingConfigurationDialog();
                        return;
                    }
                    else
                    {
                        _applicationService.ShowInformation("Issue tracking configuration cancelled.");
                        return;
                    }
                }

                string currentDesktopName = _desktopNameService.GetCurrentDesktopName();
                
                if (string.IsNullOrWhiteSpace(currentDesktopName) || currentDesktopName.StartsWith("Error:"))
                {
                    _applicationService.ShowWarning("Cannot determine current desktop name.");
                    return;
                }

                string? issueUrl = issueService.GetIssueUrlFromDesktopName(currentDesktopName);
                
                if (issueUrl == null)
                {
                    _applicationService.ShowInformation($"No issue identifier found in desktop name: \"{currentDesktopName}\"\n\n" +
                        "Make sure your desktop name contains an issue identifier that matches the configured pattern.");
                    return;
                }

                // Open the URL in the default browser
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = issueUrl,
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    _applicationService.ShowError($"Failed to open issue URL: {issueUrl}\n\nError: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                _applicationService.ShowError($"Error opening current issue: {ex.Message}");
            }
        }

        private void OnConfigureIssueTrackingClick(object? sender, EventArgs e)
        {
            try
            {
                ShowIssueTrackingConfigurationDialog();
            }
            catch (Exception ex)
            {
                _applicationService.ShowError($"Error opening issue tracking configuration: {ex.Message}");
            }
        }

        private void ShowIssueTrackingConfigurationDialog()
        {
            using (var configForm = new IssueTrackingConfigurationForm())
            {
                if (configForm.ShowDialog() == DialogResult.OK)
                {
                    ShowToastNotification("Issue tracking configuration saved.");
                }
            }
        }

        /// <summary>
        /// Checks if the clipboard contains a ticket number matching the configured pattern.
        /// </summary>
        /// <returns>The ticket number if found, null otherwise.</returns>
        private string? GetTicketNumberFromClipboard()
        {
            try
            {
                if (!_config.EnableIssueTracking || string.IsNullOrWhiteSpace(_config.IssueFormatRegex))
                {
                    return null;
                }

                if (!Clipboard.ContainsText())
                {
                    return null;
                }

                string clipboardText = Clipboard.GetText();
                var issueService = new IssueTrackingService(_config);
                return issueService.ExtractIssueFromDesktopName(clipboardText);
            }
            catch (Exception)
            {
                // If there's any error accessing clipboard or parsing, return null
                return null;
            }
        }

        /// <summary>
        /// Checks if the current desktop name already contains a ticket number.
        /// </summary>
        /// <returns>True if the current desktop has a ticket number, false otherwise.</returns>
        private bool CurrentDesktopHasTicketNumber()
        {
            try
            {
                if (!_config.EnableIssueTracking || string.IsNullOrWhiteSpace(_config.IssueFormatRegex))
                {
                    return false;
                }

                string currentDesktopName = _desktopNameService.GetCurrentDesktopName();
                var issueService = new IssueTrackingService(_config);
                string? ticketNumber = issueService.ExtractIssueFromDesktopName(currentDesktopName);
                return !string.IsNullOrEmpty(ticketNumber);
            }
            catch (Exception)
            {
                // If there's any error, assume no ticket number
                return false;
            }
        }

        /// <summary>
        /// Handles the click event for renaming desktop and updating past entries with ticket number from clipboard.
        /// </summary>
        private void OnRenameAndUpdateWithTicketClick(object? sender, EventArgs e)
        {
            try
            {
                string currentDesktopName = _desktopNameService.GetCurrentDesktopName();
                
                if (string.IsNullOrWhiteSpace(currentDesktopName) || currentDesktopName.StartsWith("Error:") || currentDesktopName == "Unknown Desktop")
                {
                    _applicationService.ShowWarning("Cannot determine current desktop name. Please ensure the desktop has a valid name.");
                    return;
                }

                string? ticketNumber = GetTicketNumberFromClipboard();
                if (string.IsNullOrEmpty(ticketNumber))
                {
                    _applicationService.ShowWarning("No valid ticket number found in clipboard.");
                    return;
                }

                // Create new name by appending ticket number
                string newName = $"{currentDesktopName} {ticketNumber}";

                // Confirm the action
                var confirmResult = MessageBox.Show(
                    $"This will:\n\n" +
                    $"1. Rename the current desktop from \"{currentDesktopName}\" to \"{newName}\"\n" +
                    $"2. Update ALL entries from TODAY with the name \"{currentDesktopName}\" to use \"{newName}\"\n\n" +
                    $"The ticket number \"{ticketNumber}\" was detected from your clipboard.\n\n" +
                    $"This action will modify your usage history. Are you sure you want to continue?",
                    "Confirm Desktop Rename & Update Past Entries",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (confirmResult != DialogResult.Yes)
                {
                    return;
                }

                // Perform the rename and update operation
                bool success = _usageTracker.UpdateDesktopNameForTodaysEntries(currentDesktopName, newName);

                if (success)
                {
                    // Update the display immediately
                    if (desktopLabel != null)
                    {
                        desktopLabel.Text = newName;
                    }
                    _trackingCoordinator.RecordDesktopName(newName);

                    ShowToastNotification($"Desktop renamed to \"{newName}\" and today's entries updated.");
                }
                else
                {
                    _applicationService.ShowError("Failed to update desktop name and past entries. Please try again.");
                }
            }
            catch (Exception ex)
            {
                _applicationService.ShowError($"Error renaming desktop with ticket number: {ex.Message}");
            }
        }

        /// <summary>
        /// Automatically renames the current desktop by appending a ticket number from clipboard.
        /// Used when Ctrl+Click is pressed and conditions are met.
        /// Also updates all past entries from today with the old name.
        /// </summary>
        /// <param name="ticketNumber">The ticket number to append to the desktop name.</param>
        private void OnAutoRenameWithTicketFromClipboard(string ticketNumber)
        {
            try
            {
                string currentDesktopName = _desktopNameService.GetCurrentDesktopName();
                
                if (string.IsNullOrWhiteSpace(currentDesktopName) || currentDesktopName.StartsWith("Error:") || currentDesktopName == "Unknown Desktop")
                {
                    _applicationService.ShowWarning("Cannot determine current desktop name. Please ensure the desktop has a valid name.");
                    return;
                }

                // Create new name by appending ticket number
                string newName = $"{currentDesktopName} {ticketNumber}";

                // Perform the rename and update operation (same as OnRenameAndUpdatePastEntriesClick)
                bool success = _usageTracker.UpdateDesktopNameForTodaysEntries(currentDesktopName, newName);

                if (success)
                {
                    // Update the display immediately
                    if (desktopLabel != null)
                    {
                        desktopLabel.Text = newName;
                    }
                    _trackingCoordinator.RecordDesktopName(newName);

                    ShowToastNotification($"Desktop renamed to \"{newName}\" with clipboard ticket.");
                }
                else
                {
                    _applicationService.ShowError("Failed to rename desktop and update past entries. Please try again.");
                }
            }
            catch (Exception ex)
            {
                _applicationService.ShowError($"Error auto-renaming desktop with ticket number: {ex.Message}");
            }
        }

        /// <summary>
        /// Copies the ticket number from the current desktop name to the clipboard.
        /// Used when Ctrl+Right-Click is pressed.
        /// </summary>
        private void OnCopyTicketNumberToClipboard()
        {
            try
            {
                if (!_config.EnableIssueTracking || string.IsNullOrWhiteSpace(_config.IssueFormatRegex))
                {
                    _applicationService.ShowWarning("Issue tracking is not configured. Please configure it first to use ticket number functionality.");
                    return;
                }

                string currentDesktopName = _desktopNameService.GetCurrentDesktopName();
                
                if (string.IsNullOrWhiteSpace(currentDesktopName) || currentDesktopName.StartsWith("Error:") || currentDesktopName == "Unknown Desktop")
                {
                    _applicationService.ShowWarning("Cannot determine current desktop name. Please ensure the desktop has a valid name.");
                    return;
                }

                var issueService = new IssueTrackingService(_config);
                string? ticketNumber = issueService.ExtractIssueFromDesktopName(currentDesktopName);

                if (string.IsNullOrEmpty(ticketNumber))
                {
                    _applicationService.ShowInformation($"No ticket number found in desktop name: \"{currentDesktopName}\"\n\nMake sure your desktop name contains a ticket number that matches the configured pattern.");
                    return;
                }

                // Copy ticket number to clipboard
                Clipboard.SetText(ticketNumber);
                
                // Show brief toast notification
                ShowToastNotification($"Ticket number \"{ticketNumber}\" copied to clipboard!");
            }
            catch (Exception ex)
            {
                _applicationService.ShowError($"Error copying ticket number to clipboard: {ex.Message}");
            }
        }

        /// <summary>
        /// Shows a brief toast notification that automatically disappears after a short duration.
        /// </summary>
        /// <param name="message">The message to display in the toast notification.</param>
        private void ShowToastNotification(string message)
        {
            _applicationService.ShowToast(this, message);
        }
    }
}
