using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using VirtualDesktopHelper;
using VirtualDesktopHelper.Configuration;
using VirtualDesktopHelper.Models;
using VirtualDesktopHelper.Services;

namespace VirtualDesktopDisplayer
{
    /// <summary>Assigns exact virtual-desktop names to Clockify projects.</summary>
    public sealed class ClockifyDesktopProjectMappingsForm : Form
    {
        private readonly ClockifyConfiguration _configuration;
        private readonly ListView _mappingList = new() { View = View.Details, FullRowSelect = true, GridLines = true };
        private readonly TextBox _desktopNameTextBox = new();
        private readonly ComboBox _projectComboBox = new() { DropDownStyle = ComboBoxStyle.DropDownList };
        private readonly Label _statusLabel = new() { AutoSize = false, ForeColor = Color.DimGray };
        private List<ClockifyProject> _projects = new();

        public List<ClockifyProjectMapping> Mappings { get; private set; }

        public ClockifyDesktopProjectMappingsForm(ClockifyConfiguration configuration)
        {
            _configuration = configuration;
            Mappings = configuration.ProjectMappings.Select(Clone).ToList();

            Text = "Clockify desktop assignments";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(650, 420);

            Controls.Add(new Label
            {
                Text = "An assigned project is used instead of the Clockify default project. Desktop names match exactly, ignoring case.",
                Location = new Point(14, 12),
                Size = new Size(620, 32)
            });

            _mappingList.Location = new Point(14, 50);
            _mappingList.Size = new Size(620, 190);
            _mappingList.Columns.Add("Desktop", 250);
            _mappingList.Columns.Add("Clockify project", 340);
            _mappingList.SelectedIndexChanged += (_, _) => LoadSelectedMapping();
            Controls.Add(_mappingList);

            Controls.Add(new Label { Text = "Desktop name:", Location = new Point(14, 258), Size = new Size(105, 23) });
            _desktopNameTextBox.Location = new Point(120, 255);
            _desktopNameTextBox.Size = new Size(325, 23);
            Controls.Add(_desktopNameTextBox);
            AddButton("Use current", 455, 253, 90, (_, _) => UseCurrentDesktop());

            Controls.Add(new Label { Text = "Clockify project:", Location = new Point(14, 294), Size = new Size(105, 23) });
            _projectComboBox.Location = new Point(120, 291);
            _projectComboBox.Size = new Size(425, 23);
            Controls.Add(_projectComboBox);

            AddButton("Assign / update", 14, 327, 120, (_, _) => AssignOrUpdate());
            AddButton("Remove", 144, 327, 80, (_, _) => RemoveSelected());
            AddButton("Reload projects", 234, 327, 110, async (_, _) => await LoadProjectsAsync());
            _statusLabel.Location = new Point(355, 327);
            _statusLabel.Size = new Size(279, 28);
            Controls.Add(_statusLabel);

            AddButton("Save assignments", 438, 377, 105, (_, _) => DialogResult = DialogResult.OK);
            AddButton("Cancel", 553, 377, 81, (_, _) => DialogResult = DialogResult.Cancel);

            RefreshMappings();
            Shown += async (_, _) => await LoadProjectsAsync();
        }

        private void AddButton(string text, int x, int y, int width, EventHandler handler)
        {
            var button = new Button { Text = text, Location = new Point(x, y), Size = new Size(width, 25) };
            button.Click += handler;
            Controls.Add(button);
        }

        private async Task LoadProjectsAsync()
        {
            try
            {
                SetStatus("Loading projects…", Color.DimGray);
                using var service = new ClockifyApiService(_configuration);
                _projects = await service.GetProjectsAsync(_configuration.WorkspaceId);
                _projectComboBox.DataSource = _projects;
                _projectComboBox.DisplayMember = nameof(ClockifyProject.DisplayName);
                SetStatus($"{_projects.Count} projects loaded.", Color.ForestGreen);
            }
            catch (Exception ex)
            {
                SetStatus($"Unable to load projects: {ex.Message}", Color.Firebrick);
            }
        }

        private void UseCurrentDesktop()
        {
            try
            {
                _desktopNameTextBox.Text = VirtualDesktopServiceProvider.GetDesktopNameService().GetCurrentDesktopName();
            }
            catch (Exception ex)
            {
                SetStatus($"Unable to get current desktop: {ex.Message}", Color.Firebrick);
            }
        }

        private void AssignOrUpdate()
        {
            string desktopName = _desktopNameTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(desktopName) || _projectComboBox.SelectedItem is not ClockifyProject project)
            {
                SetStatus("Enter a desktop name and select a project.", Color.Firebrick);
                return;
            }

            ClockifyProjectMapping? mapping = Mappings.FirstOrDefault(existing =>
                string.Equals(existing.DesktopName, desktopName, StringComparison.OrdinalIgnoreCase));
            if (mapping is null)
            {
                mapping = new ClockifyProjectMapping { DesktopName = desktopName, Order = Mappings.Count };
                Mappings.Add(mapping);
            }

            mapping.ProjectId = project.Id;
            mapping.ProjectName = project.DisplayName;
            RefreshMappings();
            SetStatus($"Assigned {desktopName}.", Color.ForestGreen);
        }

        private void RemoveSelected()
        {
            if (_mappingList.SelectedItems.Count == 0 || _mappingList.SelectedItems[0].Tag is not ClockifyProjectMapping mapping)
            {
                return;
            }

            Mappings.Remove(mapping);
            for (int index = 0; index < Mappings.Count; index++)
            {
                Mappings[index].Order = index;
            }
            RefreshMappings();
            _desktopNameTextBox.Clear();
        }

        private void LoadSelectedMapping()
        {
            if (_mappingList.SelectedItems.Count == 0 || _mappingList.SelectedItems[0].Tag is not ClockifyProjectMapping mapping)
            {
                return;
            }

            _desktopNameTextBox.Text = mapping.DesktopName;
            ClockifyProject? project = _projects.FirstOrDefault(candidate => candidate.Id == mapping.ProjectId);
            if (project is not null)
            {
                _projectComboBox.SelectedItem = project;
            }
        }

        private void RefreshMappings()
        {
            _mappingList.BeginUpdate();
            _mappingList.Items.Clear();
            foreach (ClockifyProjectMapping mapping in Mappings.OrderBy(mapping => mapping.Order))
            {
                var item = new ListViewItem(mapping.DesktopName) { Tag = mapping };
                item.SubItems.Add(mapping.ProjectName);
                _mappingList.Items.Add(item);
            }
            _mappingList.EndUpdate();
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
}
