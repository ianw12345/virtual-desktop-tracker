using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace VirtualDesktopDisplayer.Services
{
    /// <summary>
    /// Service for handling application and file operations.
    /// </summary>
    public class ApplicationService
    {
        /// <summary>
        /// Opens a file in Notepad.
        /// </summary>
        /// <param name="filePath">Path to the file to open.</param>
        /// <returns>True if successful, false otherwise.</returns>
        public bool OpenFileInNotepad(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                return false;

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "notepad.exe",
                    Arguments = $"\"{filePath}\"",
                    UseShellExecute = true
                });
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error opening file in Notepad: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Opens a folder in Windows Explorer.
        /// </summary>
        /// <param name="folderPath">Path to the folder to open.</param>
        /// <returns>True if successful, false otherwise.</returns>
        public bool OpenFolderInExplorer(string folderPath)
        {
            if (string.IsNullOrEmpty(folderPath) || !Directory.Exists(folderPath))
                return false;

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"\"{folderPath}\"",
                    UseShellExecute = true
                });
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error opening folder: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Shows an information message box.
        /// </summary>
        /// <param name="message">The message to display.</param>
        /// <param name="title">The title of the message box.</param>
        public void ShowInformation(string message, string title = "Virtual Desktop Tracker")
        {
            MessageBox.Show(message, title, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        /// <summary>
        /// Shows an error message box.
        /// </summary>
        /// <param name="message">The error message to display.</param>
        /// <param name="title">The title of the message box.</param>
        public void ShowError(string message, string title = "Virtual Desktop Tracker")
        {
            MessageBox.Show(message, title, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        /// <summary>
        /// Shows a warning message box.
        /// </summary>
        /// <param name="message">The warning message to display.</param>
        /// <param name="title">The title of the message box.</param>
        public void ShowWarning(string message, string title = "Rename Error")
        {
            MessageBox.Show(message, title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        public string? ShowTextInputDialog(IWin32Window owner, string title, string prompt, string defaultValue = "")
        {
            using var inputDialog = new Form
            {
                Width = 400,
                Height = 200,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                Text = title,
                StartPosition = FormStartPosition.CenterParent,
                MaximizeBox = false,
                MinimizeBox = false
            };

            var inputTextBox = new TextBox { Left = 10, Top = 80, Width = 360, Text = defaultValue };
            var okButton = new Button { Text = "OK", Left = 215, Width = 75, Top = 110, DialogResult = DialogResult.OK };
            var cancelButton = new Button { Text = "Cancel", Left = 295, Width = 75, Top = 110, DialogResult = DialogResult.Cancel };
            inputDialog.Controls.Add(new Label { Left = 10, Top = 10, Width = 360, Height = 60, Text = prompt });
            inputDialog.Controls.Add(inputTextBox);
            inputDialog.Controls.Add(okButton);
            inputDialog.Controls.Add(cancelButton);
            inputDialog.AcceptButton = okButton;
            inputDialog.CancelButton = cancelButton;
            inputTextBox.SelectAll();
            inputTextBox.Focus();

            return inputDialog.ShowDialog(owner) == DialogResult.OK ? inputTextBox.Text.Trim() : null;
        }

        public void ShowToast(Control owner, string message)
        {
            var toast = new Form
            {
                FormBorderStyle = FormBorderStyle.None,
                BackColor = Color.FromArgb(45, 45, 48),
                ForeColor = Color.White,
                StartPosition = FormStartPosition.Manual,
                TopMost = true,
                ShowInTaskbar = false,
                Size = new Size(300, 60),
                Text = "Virtual Desktop Tracker"
            };
            toast.Controls.Add(new Label
            {
                Text = message,
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                TextAlign = ContentAlignment.MiddleCenter,
                Dock = DockStyle.Fill,
                Padding = new Padding(10)
            });

            var screen = Screen.FromControl(owner);
            toast.Location = new Point(screen.WorkingArea.Right - toast.Width - 20, screen.WorkingArea.Bottom - toast.Height - 60);
            var timer = new System.Windows.Forms.Timer { Interval = 2000 };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                timer.Dispose();
                toast.Close();
                toast.Dispose();
            };
            toast.Show(owner);
            timer.Start();
        }

        /// <summary>
        /// Exits the application.
        /// </summary>
        public void ExitApplication()
        {
            Application.Exit();
        }
    }
}
