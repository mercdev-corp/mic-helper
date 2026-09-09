# server-tray-app Specification

## Purpose

Specifies the server system tray user interface, status representations, context menu commands, settings configuration, port collision validation, and multi-instance startup management.

## Requirements

### Requirement: Server System Tray Status Visualization
The server application SHALL display a system tray icon reflecting the current operational state: Paused, Disconnected, Microphone Muted, or Microphone Unmuted.

#### Scenario: Display muted state
- **WHEN** the monitored microphone is in a muted state and the server is active
- **THEN** the system tray shows the microphone muted icon

#### Scenario: Display device disconnected state
- **WHEN** the monitored microphone is disconnected or not found
- **THEN** the system tray shows the disconnected icon

#### Scenario: Display paused state
- **WHEN** the user pauses the server
- **THEN** the system tray shows the paused icon

### Requirement: Context Menu Navigation
The server application SHALL provide a right-click context menu on the system tray icon with Pause/Resume, Settings, and Exit options.

#### Scenario: Toggle pause and resume
- **WHEN** the user clicks Pause or Resume in the context menu
- **THEN** the server toggles its paused state, updates the tray icon, saves the paused setting immediately, and either drops clients or resumes broadcasting

#### Scenario: Open settings dialog
- **WHEN** the user clicks Settings in the context menu
- **THEN** the settings window opens while background broadcasting continues uninterrupted

#### Scenario: Exit application
- **WHEN** the user clicks Exit in the context menu
- **THEN** the application cleanly unregisters callbacks, closes the tray icon, and terminates

### Requirement: Local JSON Settings Persistence
The server application SHALL store all configuration settings in `mic-helper-server-settings.json` located strictly within the application's executable directory.

#### Scenario: Settings saved on modification
- **WHEN** any setting (microphone, port, retry timeout, paused state) is modified in the dialog or context menu
- **THEN** changes are saved immediately to `mic-helper-server-settings.json` in the local directory and applied live

#### Scenario: Isolated configuration for different directories
- **WHEN** two server executables run from different folders (e.g., `C:\Mic1` and `C:\Mic2`)
- **THEN** each server reads and writes exclusively to its own folder's `mic-helper-server-settings.json` without cross-instance interference

### Requirement: Microphone Selection with Missing Device Indication
The settings dialog SHALL provide a dropdown list of available microphones, defaulting to the system default on first run, and rendering a missing configured microphone in red strikethrough text.

#### Scenario: Configured microphone missing in dropdown
- **WHEN** the settings dialog opens and the currently saved microphone is not present on the host
- **THEN** the missing microphone is displayed as the first item with red strikethrough styling, followed by all currently connected microphones

#### Scenario: Missing microphone persists unless changed
- **WHEN** the settings dialog is closed without selecting a different microphone
- **THEN** the missing microphone remains preserved in `mic-helper-server-settings.json`

### Requirement: Port Number Configuration and In-Use Collision Detection
The settings dialog SHALL allow configuring the broadcast port (default 13205) and detect if the specified port is already bound by another process or another server instance.

#### Scenario: Port in use validation
- **WHEN** the user enters a port number that is already in use
- **THEN** the input text displays in red color and hovering over the field shows the tooltip "Port is already in use"

#### Scenario: Port change applied live
- **WHEN** a valid, available port number is entered
- **THEN** the network broadcaster re-binds to the new port and saves the setting immediately

### Requirement: Path-Unique Windows Startup Registration
The server application SHALL support automatic startup on Windows login by creating an instance-unique entry in `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` based on the executable path.

#### Scenario: Enable run on startup
- **WHEN** the user enables "Run on startup" in the settings dialog
- **THEN** the application adds a registry value named uniquely using the application type and executable path hash, pointing to the exact executable path

#### Scenario: Disable run on startup
- **WHEN** the user disables "Run on startup"
- **THEN** the application removes its specific unique registry value without affecting startup entries belonging to instances in other folders

#### Scenario: Verify startup state
- **WHEN** checking whether "Run on startup" is enabled
- **THEN** the system verifies if an entry with the exact current executable path exists under `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`

### Requirement: Per-Folder Single Instance Enforcement
The server application SHALL ensure only one instance runs from a specific directory while allowing concurrent instances from different directories.

#### Scenario: Duplicate launch in same directory
- **WHEN** a user attempts to launch a second server executable from the same directory
- **THEN** the second instance detects the existing instance via a path-scoped system mutex and exits without disrupting the running instance

#### Scenario: Concurrent launches in different directories
- **WHEN** a user launches a server executable from a different directory
- **THEN** the new instance acquires its own path-scoped mutex and starts successfully

### Requirement: Server Settings Application Version Display
The server settings dialog SHALL display the application version embedded during compilation, positioned at the bottom of the dialog between the "View Logs..." button and the "Close" button.

#### Scenario: Display current version in server settings
- **WHEN** the user opens the server settings dialog
- **THEN** the application version (e.g., `v0.1.0`) is displayed centered between the "View Logs..." and "Close" buttons, reflecting the version value defined at build time from the repository `VERSION` file.

