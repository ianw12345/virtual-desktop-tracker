# Issue Tracking Integration

The Virtual Desktop Tracker now includes issue tracking integration that allows you to:
1. Extract issue identifiers from virtual desktop names
2. Open issues directly in your browser
3. Configure custom issue patterns and URL templates

## Configuration

### Setup Issue Tracking

1. Right-click on the desktop display
2. Select **Configure → Issue Tracking**
3. Configure the following settings:

#### Enable Issue Tracking
Check this box to enable the issue tracking functionality.

#### Issue Format (Regular Expression)
Define a regex pattern that matches your issue identifiers. Examples:

- **JIRA-style**: `\b[A-Z][A-Z0-9]+-\d+\b`
  - Matches: APP-5482, PROJ-123, TICKET-9999
  
- **GitHub-style**: `#\d+`
  - Matches: #123, #4567
  
- **Custom**: `[A-Z]+-\d+`
  - Matches: APP-123, BUG-456

#### Issue URL Template
Define the URL pattern where `{0}` will be replaced with the issue identifier. Examples:

- **JIRA**: `https://yourcompany.atlassian.net/browse/{0}`
- **GitHub**: `https://github.com/youruser/yourrepo/issues/{0}`
- **Custom tracker**: `https://issuetracker.company.com/issue/{0}`

### Test Configuration
Use the test section in the configuration dialog to verify your settings:
1. Enter a sample desktop name (e.g., "Working on APP-5482 bug fix")
2. Click "Test"
3. Verify the extracted issue and generated URL

## Usage

### Open Current Issue
1. Name your virtual desktop to include an issue identifier (e.g., "APP-5482: Fix login bug")
2. Right-click on the desktop display
3. Select **Extras → Open Current Issue**
4. The issue will open in your default browser

### Desktop Naming Examples
- `APP-5482: Fix login authentication`
- `Working on PROJ-123 feature`
- `Bug fix for TICKET-9999`
- `GitHub issue #456 implementation`

## Features

- **Automatic Detection**: Automatically finds the first issue identifier in the desktop name
- **Multiple Pattern Support**: Configure any regex pattern to match your issue format
- **URL Generation**: Automatically generates issue URLs based on your template
- **Validation**: Real-time validation of regex patterns and URL templates
- **Test Mode**: Test your configuration before saving

## Error Handling

If the feature doesn't work as expected:

1. **No issue found**: Make sure your desktop name contains an issue identifier that matches your configured pattern
2. **URL doesn't open**: Verify your URL template generates valid URLs
3. **Pattern doesn't match**: Test your regex pattern in the configuration dialog

## Examples

### JIRA Integration
- **Pattern**: `\b[A-Z][A-Z0-9]+-\d+\b`
- **URL Template**: `https://yourcompany.atlassian.net/browse/{0}`
- **Desktop Name**: `APP-5482: Login bug fix`
- **Generated URL**: `https://yourcompany.atlassian.net/browse/APP-5482`

### GitHub Integration
- **Pattern**: `#\d+`
- **URL Template**: `https://github.com/youruser/yourrepo/issues/{0}`
- **Desktop Name**: `Working on issue #123`
- **Generated URL**: `https://github.com/youruser/yourrepo/issues/#123`

### Custom Tracker Integration
- **Pattern**: `\b[A-Z]{2,}-\d{3,}\b`
- **URL Template**: `https://tracker.company.com/browse/{0}`
- **Desktop Name**: `SUPPORT-12345 customer request`
- **Generated URL**: `https://tracker.company.com/browse/SUPPORT-12345`

## Configuration Storage

Issue tracking settings are stored in the application's configuration and persist across application restarts.

## Menu Integration

The feature is available from the right-click context menu:
- **Extras → Open Current Issue**: Opens the issue found in the current desktop name
- **Configure → Issue Tracking**: Opens the configuration dialog
