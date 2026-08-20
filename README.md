# Virtual Desktop Tracker

A Windows application for tracking and managing virtual desktop usage throughout the day. **VirtualDesktopDisplayer** is the single application and provides real-time virtual desktop monitoring, automatic time tracking, reports, and project detection.

## 🚀 Core Functionality

The **VirtualDesktopDisplayer** is the main application that provides:

- **Real-time Desktop Display**: Shows the current virtual desktop name in an unobtrusive corner display that stays visible across all applications
- **Automatic Time Tracking**: Tracks virtual desktop usage with detailed timestamps, duration metrics, and generates comprehensive JSON reports
- **Smart Project Detection**: Automatically detects and maps projects based on desktop name keywords
- **One-click Desktop Renaming**: Easily modify virtual desktop names with a single click
- **Issue Tracking Integration**: Extract issue identifiers from desktop names and open them directly in your browser with configurable patterns and URL templates
- **Timely Integration**: Automatically sync your desktop usage data to Timely for seamless time tracking

## 📸 Screenshots

### Main Interface
**Desktop Name Display**  
The application shows the current virtual desktop name in an unobtrusive corner display.
![Display current desktop name](img/display-current-desktop-name.png)

**Right-Click Context Menu**  
Access all application features through a convenient context menu.
![Right click options](img/right-click-options.png)

**One-Click Desktop Renaming**  
Quickly rename virtual desktops

![One-click rename virtual desktops](img/one-click-renaming-virtual-desktops.png)

### Configuration & Integration
**Timely Configuration**  
Set up the Timely integration with just a sample curl requests.
![Get curl request](img/get-curl-request.png)
![Automatic timely configuration based on curl](img/automatic-timely-configuration-based-on-curl-request.png)

**Project Detection Setup**  
Configure automatic project detection based on desktop name keywords.
![Project detection configuration based on keywords](img/project-config.png)

**Timely Upload**  
Seamlessly upload your tracked time data to Timely.
![Upload to timely](img/upload-to-timely.png)

## Useful virtual desktop shortcuts
New to Windows Virtual Desktops? I use them often, to easily switch between projects and only having the windows relevant for that project on my desktop.

Here are some shortcuts I often use:
- `Ctrl`+`Win`+`->`: Go to desktop to the right
- `Ctrl`+`Win`+`<-`: Go to desktop to the left
- `Ctrl`+`Win`+`D`: Make new desktop
- `Ctrl`+`Win`+`F4`: Close current desktop (and move open windows to the previous desktop)
- `Win`+`Tab`: Overview of windows open on current desktop (Task view)

From the task view, you can also set some windows to be always visible on all desktops. Right click on an entry and press "Show windows from this app on all desktops". I recommend this for this VirtualDesktopDisplayer (otherwise, you'll only see it on one desktop), teams and outlook. 

## 📁 Project Structure

```
virtual-desktop-tracker/
├── VirtualDesktopDisplayer/          # Main GUI application
├── VirtualDesktopHelper/             # Core library with shared functionality
├── VirtualDesktopHelper.Tests/      # Unit tests
├── VirtualDesktop/                   # External dependency (MScholtes/VirtualDesktop)
├── run-displayer.bat                # Quick start script
└── Run-VirtualDesktopDisplayer.bat  # Alternative launcher
```

## 🛠️ Prerequisites

### Required Dependencies

1. **Clone the VirtualDesktop library** in the same directory:
   ```bash
   git clone https://github.com/MScholtes/VirtualDesktop.git
   ```
   Compile the VirtualDesktop project for your Windows version:
   ```bash
   cd VirtualDesktop
   C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe VirtualDesktop11.cs
   ```

2. **System Requirements**:
   - Windows 10/11 with Virtual Desktop support
   - .NET 9.0 or later

### Directory Structure
After cloning, your directory should look like:
```
your-project-folder/
├── virtual-desktop-tracker/    # This repository
└── VirtualDesktop/            # MScholtes' VirtualDesktop repository
```

## 🚀 Quick Start

### Method 1: Using Batch File
```bash
# Run the main application
.\run-displayer.bat
```

### Method 2: Build and Run
```bash
# Build the solution
dotnet build

# Run the displayer
cd VirtualDesktopDisplayer
dotnet run
```

### Method 3: Run Pre-built Executable
```bash
cd VirtualDesktopDisplayer\bin\Debug\net9.0-windows
.\VirtualDesktopDisplayer.exe
```

## 💡 How to Use

1. **Start the Application**: Use any of the methods above to launch the VirtualDesktopDisplayer

2. **Desktop Display**: You'll see the current virtual desktop name appear in the bottom-right corner of your screen

3. **Automatic Tracking**: The application automatically starts tracking your desktop usage in the background

4. **Rename Desktops**: Click on the desktop name display to quickly rename your virtual desktop

5. **View Reports**: Right-click on the display for options to:
   - View usage logs
   - Generate date-specific reports, including the former command-line consolidation options
   - Estimate working hours for any selected date
   - View the current tracking status
   - Open log folder
   - Configure settings
   - Exit application

6. **Exit**: Double-click the display or right-click → Exit

## 📊 Usage Reports

The application generates detailed JSON reports with information like:

```json
{
  "GeneratedAt": "2025-08-24 10:30:00",
  "TotalActivities": 15,
  "Activities": [
    {
      "DesktopName": "Development Work",
      "StartTime": "2025-08-24 09:00:00",
      "EndTime": "2025-08-24 10:15:00",
      "DurationFormatted": "1h 15m",
      "Date": "2025-08-24"
    }
  ]
}
```

Reports are automatically saved to the `VirtualDesktopLogs` directory in your Documents folder. 

## ⚙️ Configuration

### Project Detection
Configure automatic project detection by editing keywords in the project configuration:

```csharp
// Example project mapping
{
  "Project": { "Id": 12345, "Name": "My Project" },
  "Keywords": ["keyword1", "keyword2"]
}
```

### Timely Integration
Set up Timely integration for automatic time tracking by configuring:
- API credentials
- Workspace ID
- Default project mappings

### Issue Tracking Integration
Configure issue tracking integration to link desktop names with your issue tracker:
- Define custom regex patterns to match issue identifiers (e.g., `APP-5482`, `#123`)
- Set URL templates for your issue tracker (JIRA, GitHub, etc.)
- One-click access to issues from desktop names

For detailed setup instructions, see [ISSUE_TRACKING.md](ISSUE_TRACKING.md).

## 🏗️ Architecture

### Components

- **VirtualDesktopDisplayer**: The single WinForms application for display, tracking, analysis, and configuration
- **VirtualDesktopHelper**: Core library containing:
  - Desktop name detection
  - Usage tracking services
  - Project configuration
  - Timely integration
  - Screen state detection

### Key Services

- `IWindowsDesktopNameService`: Retrieves current desktop names
- `IDesktopUsageTracker`: Tracks and logs desktop usage
- `IScreenStateDetector`: Detects screen lock/unlock events
- `IUsageConsolidationService`: Consolidates and processes usage data

## 🧪 Testing

Run the test suite:
```bash
dotnet test
```

The test suite includes:
- Unit tests for all services
- Integration tests for desktop detection
- Performance tests for tracking accuracy

## 🤝 Contributing

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

## 📝 Dependencies

- **External**: [MScholtes/VirtualDesktop](https://github.com/MScholtes/VirtualDesktop) - Provides the core Windows virtual desktop API access
- **Internal**: .NET 9.0, Windows Forms, System.Text.Json

## 📄 License

This project is licensed under the MIT License - see the individual component licenses for details.

## 🐛 Troubleshooting

### Common Issues

**Desktop name shows as "Error: ..."**
- Ensure the VirtualDesktop folder is cloned in the correct location
- Verify that VirtualDesktop11.exe exists and is executable

**Failed to rename desktop, please try again**
- Ensure the correct VirtualDesktop executable for your Windows version is configured in `TrackerConfiguration.cs`. You might need `VirtualDesktop11-24H2.exe` instead of `VirtualDesktop11.exe`.

**Program only visible on the initial desktop**
- From the task view (`Win`+`Tab`), right click on the VirtualDesktopDisplayer and click on "Show Windows from this app on all desktops"
