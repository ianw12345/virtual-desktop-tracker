using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Globalization;
using VirtualDesktopHelper.Services;
using VirtualDesktopHelper.Interfaces;
using VirtualDesktopHelper.Configuration;

namespace VirtualDesktopTracker
{
	class Program
	{
		static async Task<int> Main(string[] args)
		{
			try
			{
				// Check if this is a report generation request
				if (args.Length > 0 && (args[0].Equals("report", StringComparison.OrdinalIgnoreCase) || 
				                        args[0].Equals("--report", StringComparison.OrdinalIgnoreCase) || 
				                        args[0].Equals("-r", StringComparison.OrdinalIgnoreCase)))
				{
					await GenerateReportFromArgs(args);
					return 0;
				}

				// Check if this is a working hours estimation request
				if (args.Length > 0 && (args[0].Equals("hours", StringComparison.OrdinalIgnoreCase) || 
				                        args[0].Equals("--hours", StringComparison.OrdinalIgnoreCase) || 
				                        args[0].Equals("-w", StringComparison.OrdinalIgnoreCase)))
				{
					await EstimateWorkingHoursFromArgs(args);
					return 0;
				}

				// Show help if requested
				if (args.Length > 0 && (args[0].Equals("--help", StringComparison.OrdinalIgnoreCase) || 
				                        args[0].Equals("-h", StringComparison.OrdinalIgnoreCase) ||
				                        args[0].Equals("help", StringComparison.OrdinalIgnoreCase)))
				{
					ShowHelp();
					return 0;
				}

				// Default behavior: start tracking
				await StartTrackingAsync();
				return 0;
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Error: {ex.Message}");
				return 1;
			}
		}

		static void ShowHelp()
		{
			Console.WriteLine("Virtual Desktop Tracker");
			Console.WriteLine();
			Console.WriteLine("Usage:");
			Console.WriteLine("  VirtualDesktopTracker                     Start desktop tracking");
			Console.WriteLine("  VirtualDesktopTracker report [options]    Generate usage report");
			Console.WriteLine("  VirtualDesktopTracker hours [options]     Estimate working hours");
			Console.WriteLine("  VirtualDesktopTracker help                Show this help");
			Console.WriteLine();
			Console.WriteLine("Report Options:");
			Console.WriteLine("  --date <YYYY-MM-DD>           Generate report for specific date (default: today)");
			Console.WriteLine("  --consolidate <true|false>    Apply consolidation rules (default: true)");
			Console.WriteLine("  --min-duration <minutes>      Minimum activity duration to keep (default: 2.0)");
			Console.WriteLine("  --max-duration <minutes>      Max duration for custom consolidation (default: 15.0)");
			Console.WriteLine();
			Console.WriteLine("Working Hours Options:");
			Console.WriteLine("  --date <YYYY-MM-DD>           Estimate hours for specific date (default: today)");
			Console.WriteLine();
			Console.WriteLine("Examples:");
			Console.WriteLine("  VirtualDesktopTracker report --date 2025-08-22");
			Console.WriteLine("  VirtualDesktopTracker report --date 2025-08-22 --consolidate false");
			Console.WriteLine("  VirtualDesktopTracker report --min-duration 5.0 --max-duration 20.0");
			Console.WriteLine("  VirtualDesktopTracker hours");
			Console.WriteLine("  VirtualDesktopTracker hours --date 2025-08-22");
		}

		static async Task StartTrackingAsync()
		{
			Console.WriteLine("Virtual Desktop Tracker Started");
			Console.WriteLine("Press Ctrl+C to stop tracking...");
			
			var usageTracker = new DesktopUsageTracker();
			var config = TrackerConfiguration.Instance;
			var screenStateDetector = new WindowsScreenStateDetector();
			var errorHandler = new VirtualDesktopErrorHandler(config);
			var desktopNameService = new WindowsDesktopNameService(screenStateDetector, errorHandler, config);
			var trackingCoordinator = new DesktopTrackingCoordinator(desktopNameService, screenStateDetector, usageTracker);
			using var cancellationSource = new CancellationTokenSource();
			Console.WriteLine($"Log directory: {usageTracker.GetLogDirectory()}");
			Console.WriteLine($"Current log file: {Path.GetFileName(usageTracker.GetCurrentLogFilePath())}");
			Console.WriteLine();

			ConsoleCancelEventHandler cancelHandler = (sender, e) =>
			{
				e.Cancel = true;
				Console.WriteLine("\nShutting down tracker...");
				cancellationSource.Cancel();
			};

			Console.CancelKeyPress += cancelHandler;
			try
			{
				await TrackDesktopChangesAsync(trackingCoordinator, config, cancellationSource.Token);
			}
			finally
			{
				Console.CancelKeyPress -= cancelHandler;
				trackingCoordinator.Stop();
				Console.WriteLine("Last session closed. Tracker stopped.");
			}
		}

		static async Task GenerateReportFromArgs(string[] args)
		{
			DateTime targetDate = DateTime.Today;
			bool consolidate = true;
			double minDurationMinutes = 2.0;
			double maxDurationMinutes = 15.0;

			// Parse command line arguments
			for (int i = 1; i < args.Length; i++)
			{
				switch (args[i].ToLower())
				{
					case "--date":
					case "-d":
						if (i + 1 < args.Length)
						{
							if (DateTime.TryParseExact(args[i + 1], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsedDate))
							{
								targetDate = parsedDate;
								i++; // Skip the next argument as it's the date value
							}
							else
							{
								Console.WriteLine($"Invalid date format: {args[i + 1]}. Use YYYY-MM-DD format.");
								return;
							}
						}
						break;

					case "--consolidate":
					case "-c":
						if (i + 1 < args.Length)
						{
							if (bool.TryParse(args[i + 1], out bool consolidateValue))
							{
								consolidate = consolidateValue;
								i++; // Skip the next argument as it's the consolidate value
							}
							else
							{
								Console.WriteLine($"Invalid consolidate value: {args[i + 1]}. Use true or false.");
								return;
							}
						}
						break;

					case "--min-duration":
					case "-m":
						if (i + 1 < args.Length)
						{
							if (double.TryParse(args[i + 1], out double minDuration))
							{
								minDurationMinutes = minDuration;
								i++; // Skip the next argument as it's the duration value
							}
							else
							{
								Console.WriteLine($"Invalid min-duration value: {args[i + 1]}. Use a number of minutes.");
								return;
							}
						}
						break;

					case "--max-duration":
					case "-x":
						if (i + 1 < args.Length)
						{
							if (double.TryParse(args[i + 1], out double maxDuration))
							{
								maxDurationMinutes = maxDuration;
								i++; // Skip the next argument as it's the duration value
							}
							else
							{
								Console.WriteLine($"Invalid max-duration value: {args[i + 1]}. Use a number of minutes.");
								return;
							}
						}
						break;

					default:
						Console.WriteLine($"Unknown option: {args[i]}");
						ShowHelp();
						return;
				}
			}

			await GenerateReport(targetDate, consolidate, minDurationMinutes, maxDurationMinutes);
		}

		static async Task GenerateReport(DateTime targetDate, bool consolidate, double minDurationMinutes, double maxDurationMinutes)
		{
			try
			{
				Console.WriteLine($"Generating usage report for {targetDate:yyyy-MM-dd}");
				Console.WriteLine($"Consolidation: {(consolidate ? "Enabled" : "Disabled")}");
				if (consolidate)
				{
					Console.WriteLine($"Min duration: {minDurationMinutes} minutes");
					Console.WriteLine($"Max custom duration: {maxDurationMinutes} minutes");
				}
				Console.WriteLine();

				// Create configuration with consolidation settings
				var config = new TrackerConfiguration();
				config.EnableActivityConsolidation = consolidate;
				config.ConsolidationMinDurationMinutes = minDurationMinutes;
				config.CustomConsolidationMaxDurationMinutes = maxDurationMinutes;

				// Get the services
				var usageTracker = new DesktopUsageTracker();
				var reportGenerator = new UsageReportGenerator(config);

				// Get usage data for the specified date with properly closed sessions
				var usageHistory = usageTracker.GetAllUsageHistoryWithClosedSessions()
					.Where(entry => entry.StartTime.Date == targetDate.Date)
					.ToList();
				
				if (usageHistory == null || usageHistory.Count == 0)
				{
					Console.WriteLine($"No usage data found for {targetDate:yyyy-MM-dd}");
					return;
				}

				Console.WriteLine($"Found {usageHistory.Count} usage entries for {targetDate:yyyy-MM-dd}");

				// Also save to file and generate JSON
				string reportFileName = $"usage_report_{targetDate:yyyy-MM-dd}.txt";
				string reportPath = Path.Combine(usageTracker.GetLogDirectory(), reportFileName);
				
				// Generate both text and JSON reports
				string report = await reportGenerator.GenerateReportWithJsonAsync(usageHistory, reportPath);
				
				Console.WriteLine();
				Console.WriteLine("=== USAGE REPORT ===");
				Console.WriteLine(report);

				Console.WriteLine();
				Console.WriteLine($"Text report saved to: {reportPath}");
				Console.WriteLine($"JSON report saved to: {Path.ChangeExtension(reportPath, ".json")}");
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Error generating report: {ex.Message}");
				if (ex.InnerException != null)
				{
					Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
				}
			}
		}

		static async Task EstimateWorkingHoursFromArgs(string[] args)
		{
			DateTime targetDate = DateTime.Today;

			// Parse command line arguments
			for (int i = 1; i < args.Length; i++)
			{
				switch (args[i].ToLower())
				{
					case "--date":
					case "-d":
						if (i + 1 < args.Length)
						{
							if (DateTime.TryParseExact(args[i + 1], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsedDate))
							{
								targetDate = parsedDate;
								i++; // Skip the next argument as it's the date value
							}
							else
							{
								Console.WriteLine($"Invalid date format: {args[i + 1]}. Use YYYY-MM-DD format.");
								return;
							}
						}
						break;

					default:
						Console.WriteLine($"Unknown option: {args[i]}");
						ShowHelp();
						return;
				}
			}

			await EstimateWorkingHours(targetDate);
		}

		static async Task EstimateWorkingHours(DateTime targetDate)
		{
			try
			{
				Console.WriteLine($"Estimating working hours for {targetDate:yyyy-MM-dd}");
				Console.WriteLine();

				// Create usage tracker
				var usageTracker = new DesktopUsageTracker();
				var usageHistory = usageTracker.GetAllUsageHistory();

				// Create working hours estimation service
				var estimationService = new WorkingHoursEstimationService();
				var estimation = estimationService.EstimateWorkingHours(usageHistory, targetDate);

				// Display results
				Console.WriteLine("=== WORKING HOURS ESTIMATION ===");
				Console.WriteLine($"Date: {estimation.Date:yyyy-MM-dd}");
				Console.WriteLine();
				Console.WriteLine($"Total worked: {FormatHours(estimation.TotalWorkedHours)} / {FormatHours(7.33)} (7h 20m)");
				Console.WriteLine($"Hours remaining: {FormatHours(estimation.HoursRemaining)}");
				
				if (estimation.LunchBreak != null)
				{
					Console.WriteLine($"Lunch break: {estimation.LunchBreak.StartTime:HH:mm} - {estimation.LunchBreak.EndTime:HH:mm} ({FormatTimeSpan(estimation.LunchBreak.Duration)})");
				}
				else
				{
					Console.WriteLine("Lunch break: Not detected (no Screen Off 20+ min between 11:45-13:15)");
				}

				if (estimation.EstimatedFinishTime.HasValue)
				{
					Console.WriteLine($"Estimated finish time: {estimation.EstimatedFinishTime.Value:HH:mm}");
				}
				
				Console.WriteLine();
				if (estimation.HoursRemaining <= 0)
				{
					Console.WriteLine("✅ You've completed your working hours for today!");
				}
				else
				{
					Console.WriteLine($"⏰ {FormatHours(estimation.HoursRemaining)} remaining to complete today's work");
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Error estimating working hours: {ex.Message}");
				if (ex.InnerException != null)
				{
					Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
				}
			}
		}

		static string FormatHours(double hours)
		{
			var totalMinutes = (int)(hours * 60);
			var h = totalMinutes / 60;
			var m = totalMinutes % 60;
			
			if (h == 0) return $"{m}m";
			if (m == 0) return $"{h}h";
			return $"{h}h {m}m";
		}

		static string FormatTimeSpan(TimeSpan timeSpan)
		{
			var totalMinutes = (int)timeSpan.TotalMinutes;
			var h = totalMinutes / 60;
			var m = totalMinutes % 60;
			
			if (h == 0) return $"{m}m";
			if (m == 0) return $"{h}h";
			return $"{h}h {m}m";
		}

		static async Task TrackDesktopChangesAsync(
			DesktopTrackingCoordinator trackingCoordinator,
			TrackerConfiguration config,
			CancellationToken cancellationToken)
		{
			while (!cancellationToken.IsCancellationRequested)
			{
				var trackingUpdate = trackingCoordinator.Poll();
				if (!string.IsNullOrEmpty(trackingUpdate.ErrorMessage))
				{
					Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Error: {trackingUpdate.ErrorMessage}");
				}
				else if (trackingUpdate.HasChanged)
				{
					if (string.IsNullOrEmpty(trackingUpdate.PreviousDesktopName))
					{
						Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Initial desktop: {trackingUpdate.DesktopName}");
					}
					else
					{
						Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Desktop changed: {trackingUpdate.PreviousDesktopName} -> {trackingUpdate.DesktopName}");
					}
				}

				TimeSpan interval = trackingUpdate.IsScreenOff
					? config.InactiveScreenUpdateInterval
					: config.ActiveScreenUpdateInterval;
				try
				{
					await Task.Delay(interval, cancellationToken);
				}
				catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
				{
					break;
				}
			}
		}
	}
}
