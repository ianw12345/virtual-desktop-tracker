using System;
using System.Windows.Forms;
using VirtualDesktopHelper.Services;

namespace VirtualDesktopDisplayer
{
    /// <summary>
    /// Collects report settings that were previously command-line switches.
    /// </summary>
    public sealed class UsageReportOptionsForm : Form
    {
        private readonly DateTimePicker _datePicker;
        private readonly CheckBox _consolidateCheckBox;
        private readonly NumericUpDown _minimumDurationInput;
        private readonly NumericUpDown _maximumDurationInput;

        public UsageReportOptions Options { get; private set; } = new(DateTime.Today);

        public UsageReportOptionsForm()
        {
            Text = "Generate Usage Report";
            Size = new System.Drawing.Size(390, 250);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;

            _datePicker = new DateTimePicker { Left = 180, Top = 18, Width = 170, Format = DateTimePickerFormat.Short, Value = DateTime.Today };
            _consolidateCheckBox = new CheckBox { Left = 18, Top = 58, Width = 250, Text = "Apply activity consolidation", Checked = true };
            _minimumDurationInput = CreateDurationInput(180, 94, 2);
            _maximumDurationInput = CreateDurationInput(180, 130, 15);
            _consolidateCheckBox.CheckedChanged += (_, _) => UpdateDurationInputs();

            var generateButton = new Button { Text = "Generate", Left = 194, Top = 170, Width = 75, DialogResult = DialogResult.OK };
            generateButton.Click += (_, _) => Options = new UsageReportOptions(
                _datePicker.Value.Date,
                _consolidateCheckBox.Checked,
                (double)_minimumDurationInput.Value,
                (double)_maximumDurationInput.Value);
            var cancelButton = new Button { Text = "Cancel", Left = 275, Top = 170, Width = 75, DialogResult = DialogResult.Cancel };

            Controls.AddRange(new Control[]
            {
                new Label { Text = "Report date:", Left = 18, Top = 22, Width = 150 }, _datePicker,
                _consolidateCheckBox,
                new Label { Text = "Minimum duration (minutes):", Left = 18, Top = 98, Width = 155 }, _minimumDurationInput,
                new Label { Text = "Maximum custom duration:", Left = 18, Top = 134, Width = 155 }, _maximumDurationInput,
                generateButton, cancelButton
            });
            AcceptButton = generateButton;
            CancelButton = cancelButton;
        }

        private static NumericUpDown CreateDurationInput(int left, int top, decimal value) => new()
        {
            Left = left,
            Top = top,
            Width = 170,
            DecimalPlaces = 1,
            Increment = 0.5m,
            Minimum = 0,
            Maximum = 1440,
            Value = value
        };

        private void UpdateDurationInputs()
        {
            _minimumDurationInput.Enabled = _consolidateCheckBox.Checked;
            _maximumDurationInput.Enabled = _consolidateCheckBox.Checked;
        }
    }
}
