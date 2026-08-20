using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using VirtualDesktopHelper.Configuration;
using VirtualDesktopHelper.Models;
using VirtualDesktopHelper.Services;

namespace VirtualDesktopDisplayer
{
    /// <summary>Configures the native Clockify time-entry integration.</summary>
    public sealed class ClockifyConfigurationForm : Form
    {
        private readonly ClockifyConfiguration _configuration = ClockifyConfiguration.Instance;
        private readonly TextBox _apiKeyTextBox = new() { UseSystemPasswordChar = true };
        private readonly TextBox _workspaceIdTextBox = new();
        private readonly TextBox _projectIdTextBox = new();
        private readonly TextBox _projectNameTextBox = new() { ReadOnly = true };
        private readonly CheckBox _billableCheckBox = new() { Text = "Create entries as billable" };
        private readonly Label _statusLabel = new() { AutoSize = false, ForeColor = Color.DimGray };
        private List<ClockifyProjectMapping> _projectMappings = new();

        public ClockifyConfigurationForm()
        {
            Text = "Clockify configuration";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(590, 310);

            var description = new Label
            {
                Text = "Create an API key in Clockify under Profile settings. It is encrypted for your Windows user when saved.",
                Location = new Point(16, 14),
                Size = new Size(556, 36)
            };
            Controls.Add(description);

            AddField("API key:", _apiKeyTextBox, 58, 430);
            AddButton("Test connection", 462, 56, 110, async (_, _) => await TestConnectionAsync());

            AddField("Workspace ID:", _workspaceIdTextBox, 96, 330);
            AddButton("Choose workspace", 432, 94, 140, async (_, _) => await ChooseWorkspaceAsync());

            AddField("Default project ID:", _projectIdTextBox, 134, 330);
            AddButton("Choose project", 432, 132, 140, async (_, _) => await ChooseProjectAsync());

            AddField("Selected project:", _projectNameTextBox, 172, 430);
            _billableCheckBox.Location = new Point(140, 210);
            _billableCheckBox.Size = new Size(260, 24);
            Controls.Add(_billableCheckBox);
            AddButton("Desktop assignments…", 408, 208, 164, (_, _) => ManageDesktopAssignments());

            _statusLabel.Location = new Point(16, 242);
            _statusLabel.Size = new Size(556, 28);
            Controls.Add(_statusLabel);

            AddButton("Save", 390, 276, 85, (_, _) => SaveConfiguration());
            AddButton("Cancel", 487, 276, 85, (_, _) => DialogResult = DialogResult.Cancel);

            _apiKeyTextBox.Text = _configuration.ApiKey;
            _workspaceIdTextBox.Text = _configuration.WorkspaceId;
            _projectIdTextBox.Text = _configuration.DefaultProjectId;
            _projectNameTextBox.Text = _configuration.DefaultProjectName;
            _billableCheckBox.Checked = _configuration.IsBillable;
            _projectMappings = _configuration.ProjectMappings.Select(Clone).ToList();
        }

        private void AddField(string labelText, TextBox textBox, int y, int width)
        {
            Controls.Add(new Label { Text = labelText, Location = new Point(16, y + 4), Size = new Size(118, 23) });
            textBox.Location = new Point(140, y);
            textBox.Size = new Size(width, 23);
            Controls.Add(textBox);
        }

        private void AddButton(string text, int x, int y, int width, EventHandler clickHandler)
        {
            var button = new Button { Text = text, Location = new Point(x, y), Size = new Size(width, 25) };
            button.Click += clickHandler;
            Controls.Add(button);
        }

        private ClockifyConfiguration CreateDraftConfiguration() => new()
        {
            ApiKey = _apiKeyTextBox.Text.Trim(),
            WorkspaceId = _workspaceIdTextBox.Text.Trim(),
            DefaultProjectId = _projectIdTextBox.Text.Trim(),
            DefaultProjectName = _projectNameTextBox.Text.Trim(),
            IsBillable = _billableCheckBox.Checked,
            ProjectMappings = _projectMappings.Select(Clone).ToList()
        };

        private void ManageDesktopAssignments()
        {
            ClockifyConfiguration draft = CreateDraftConfiguration();
            if (string.IsNullOrWhiteSpace(draft.ApiKey) || string.IsNullOrWhiteSpace(draft.WorkspaceId))
            {
                SetStatus("Enter an API key and workspace before assigning desktop projects.", Color.Firebrick);
                return;
            }

            using var mappingsForm = new ClockifyDesktopProjectMappingsForm(draft);
            if (mappingsForm.ShowDialog(this) == DialogResult.OK)
            {
                _projectMappings = mappingsForm.Mappings.Select(Clone).ToList();
                SetStatus($"{_projectMappings.Count} desktop assignment(s) ready to save.", Color.ForestGreen);
            }
        }

        private async Task TestConnectionAsync()
        {
            try
            {
                SetStatus("Testing connection…", Color.DimGray);
                using var service = new ClockifyApiService(CreateDraftConfiguration());
                await service.TestConnectionAsync();
                SetStatus("Clockify connection successful.", Color.ForestGreen);
            }
            catch (Exception ex)
            {
                SetStatus($"Connection failed: {ex.Message}", Color.Firebrick);
            }
        }

        private async Task ChooseWorkspaceAsync()
        {
            try
            {
                using var service = new ClockifyApiService(CreateDraftConfiguration());
                List<ClockifyWorkspace> workspaces = await service.GetWorkspacesAsync();
                if (workspaces.Count == 0)
                {
                    SetStatus("No Clockify workspaces are available for this API key.", Color.Firebrick);
                    return;
                }

                using var selector = new ClockifySelectionForm<ClockifyWorkspace>("Choose Clockify workspace", workspaces);
                if (selector.ShowDialog(this) == DialogResult.OK && selector.SelectedItem is not null)
                {
                    _workspaceIdTextBox.Text = selector.SelectedItem.Id;
                    SetStatus($"Workspace selected: {selector.SelectedItem.Name}", Color.ForestGreen);
                }
            }
            catch (Exception ex)
            {
                SetStatus($"Unable to load workspaces: {ex.Message}", Color.Firebrick);
            }
        }

        private async Task ChooseProjectAsync()
        {
            if (string.IsNullOrWhiteSpace(_workspaceIdTextBox.Text))
            {
                SetStatus("Choose a workspace before loading projects.", Color.Firebrick);
                return;
            }

            try
            {
                using var service = new ClockifyApiService(CreateDraftConfiguration());
                List<ClockifyProject> projects = await service.GetProjectsAsync(_workspaceIdTextBox.Text.Trim());
                if (projects.Count == 0)
                {
                    SetStatus("No active Clockify projects are available in this workspace.", Color.Firebrick);
                    return;
                }

                using var selector = new ClockifySelectionForm<ClockifyProject>("Choose Clockify project", projects);
                if (selector.ShowDialog(this) == DialogResult.OK && selector.SelectedItem is not null)
                {
                    _projectIdTextBox.Text = selector.SelectedItem.Id;
                    _projectNameTextBox.Text = selector.SelectedItem.DisplayName;
                    SetStatus($"Project selected: {selector.SelectedItem.DisplayName}", Color.ForestGreen);
                }
            }
            catch (Exception ex)
            {
                SetStatus($"Unable to load projects: {ex.Message}", Color.Firebrick);
            }
        }

        private void SaveConfiguration()
        {
            ClockifyConfiguration draft = CreateDraftConfiguration();
            if (!draft.IsConfigured())
            {
                SetStatus("API key, workspace and default project are required.", Color.Firebrick);
                return;
            }

            _configuration.ApiKey = draft.ApiKey;
            _configuration.WorkspaceId = draft.WorkspaceId;
            _configuration.DefaultProjectId = draft.DefaultProjectId;
            _configuration.DefaultProjectName = draft.DefaultProjectName;
            _configuration.IsBillable = draft.IsBillable;
            _configuration.ProjectMappings = draft.ProjectMappings;
            _configuration.SaveConfiguration();
            DialogResult = DialogResult.OK;
        }

        private void SetStatus(string text, Color color)
        {
            _statusLabel.Text = text;
            _statusLabel.ForeColor = color;
        }

        private static ClockifyProjectMapping Clone(ClockifyProjectMapping mapping) => new()
        {
            DesktopName = mapping.DesktopName,
            ProjectId = mapping.ProjectId,
            ProjectName = mapping.ProjectName,
            Order = mapping.Order
        };
    }

    internal sealed class ClockifySelectionForm<T> : Form where T : class
    {
        private readonly ListBox _listBox = new() { Dock = DockStyle.Fill };
        public T? SelectedItem => _listBox.SelectedItem as T;

        public ClockifySelectionForm(string title, IEnumerable<T> items)
        {
            Text = title;
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(470, 390);
            MinimizeBox = false;

            _listBox.Items.AddRange(items.Cast<object>().ToArray());
            _listBox.DoubleClick += (_, _) => Confirm();

            var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 42, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(8) };
            var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Width = 82 };
            var select = new Button { Text = "Select", Width = 82 };
            select.Click += (_, _) => Confirm();
            buttons.Controls.Add(cancel);
            buttons.Controls.Add(select);
            Controls.Add(_listBox);
            Controls.Add(buttons);
            AcceptButton = select;
            CancelButton = cancel;
        }

        private void Confirm()
        {
            if (SelectedItem is not null)
            {
                DialogResult = DialogResult.OK;
            }
        }
    }
}
