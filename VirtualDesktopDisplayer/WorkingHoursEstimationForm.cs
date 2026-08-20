using System;
using System.Windows.Forms;
using VirtualDesktopHelper.Services;

namespace VirtualDesktopDisplayer
{
    /// <summary>
    /// Displays a working-hours estimation for a user-selected date.
    /// </summary>
    public sealed class WorkingHoursEstimationForm : Form
    {
        private readonly UsageAnalysisService _usageAnalysisService;
        private readonly DateTimePicker _datePicker;
        private readonly TextBox _resultTextBox;

        public WorkingHoursEstimationForm(UsageAnalysisService usageAnalysisService)
        {
            _usageAnalysisService = usageAnalysisService ?? throw new ArgumentNullException(nameof(usageAnalysisService));

            Text = "Working Hours Estimation";
            Size = new System.Drawing.Size(520, 410);
            MinimumSize = Size;
            StartPosition = FormStartPosition.CenterParent;

            _datePicker = new DateTimePicker
            {
                Format = DateTimePickerFormat.Short,
                Value = DateTime.Today,
                Left = 16,
                Top = 16,
                Width = 130
            };
            var calculateButton = new Button { Text = "Calculate", Left = 160, Top = 15, Width = 100 };
            calculateButton.Click += (_, _) => ShowEstimation();

            _resultTextBox = new TextBox
            {
                Left = 16,
                Top = 54,
                Width = 472,
                Height = 290,
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical
            };
            var closeButton = new Button
            {
                Text = "Close",
                Left = 413,
                Top = 352,
                Width = 75,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
                DialogResult = DialogResult.Cancel
            };

            Controls.AddRange(new Control[] { _datePicker, calculateButton, _resultTextBox, closeButton });
            CancelButton = closeButton;
            Shown += (_, _) => ShowEstimation();
        }

        private void ShowEstimation()
        {
            try
            {
                WorkingHoursEstimation estimation = _usageAnalysisService.EstimateWorkingHours(_datePicker.Value.Date);
                _resultTextBox.Text = CreateSummary(estimation);
            }
            catch (Exception ex)
            {
                _resultTextBox.Text = $"Error estimating working hours: {ex.Message}";
            }
        }

        private static string CreateSummary(WorkingHoursEstimation estimation)
        {
            string summary = $"Date: {estimation.Date:yyyy-MM-dd}\r\n\r\n" +
                $"Total worked: {FormatHours(estimation.TotalWorkedHours)} / 7h 20m\r\n" +
                $"Hours remaining: {FormatHours(estimation.HoursRemaining)}\r\n";

            summary += estimation.LunchBreak is null
                ? "Lunch break: Not detected (no Screen Off 20+ min between 11:45-13:15)\r\n"
                : $"Lunch break: {estimation.LunchBreak.StartTime:HH:mm} - {estimation.LunchBreak.EndTime:HH:mm} ({FormatTimeSpan(estimation.LunchBreak.Duration)})\r\n";

            if (estimation.EstimatedFinishTime.HasValue)
            {
                summary += $"Estimated finish time: {estimation.EstimatedFinishTime.Value:HH:mm}\r\n";
            }

            return summary + "\r\n" + estimation.Message;
        }

        private static string FormatHours(double hours) => FormatTimeSpan(TimeSpan.FromHours(Math.Max(0, hours)));

        private static string FormatTimeSpan(TimeSpan duration)
        {
            int totalMinutes = (int)Math.Round(duration.TotalMinutes, MidpointRounding.AwayFromZero);
            return totalMinutes < 60 ? $"{totalMinutes}m" : $"{totalMinutes / 60}h {totalMinutes % 60}m";
        }
    }
}
