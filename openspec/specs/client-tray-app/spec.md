# client-tray-app Specification

## Purpose

Specifies the client system tray user interface, status visualizer, context menu, WYSIWYG overlay positioning controller, settings configuration, and multi-instance startup management.

## Requirements

### Requirement: Client System Tray Status Visualization
The client application SHALL display a system tray icon reflecting the current operational state: Paused, Disconnected (cannot reach server), Microphone Muted, or Microphone Unmuted.

#### Scenario: Display muted state in tray
- **WHEN** connected to the server and the remote microphone is muted
- **THEN** the system tray shows the microphone muted status icon

#### Scenario: Display server disconnected in tray
- **WHEN** the server is unreachable or timed out
- **THEN** the system tray shows the disconnected status icon

#### Scenario: Display client paused state in tray
- **WHEN** the user pauses the client application
- **THEN** the system tray shows the paused status icon

### Requirement: Context Menu Navigation
The client application SHALL provide a right-click context menu on the system tray icon with Pause/Resume, Settings, and Exit options.

#### Scenario: Toggle pause and resume
- **WHEN** the user clicks Pause or Resume in the context menu
- **THEN** the client toggles its paused state, updates the tray icon, hides overlay if paused, saves the paused setting immediately, and suspends or resumes server packet consumption

#### Scenario: Open settings dialog
- **WHEN** the user clicks Settings in the context menu
- **THEN** the movable settings window opens and the overlay switches into interactive WYSIWYG controller mode

#### Scenario: Exit application
- **WHEN** the user clicks Exit in the context menu
- **THEN** the client destroys the overlay window, removes the system tray icon, and exits cleanly

### Requirement: WYSIWYG Overlay Interactive Positioning and Resizing
While the settings dialog is open, the overlay SHALL become visible and function as a direct WYSIWYG manipulator allowing the user to reposition and resize the overlay across screens.

#### Scenario: Drag to reposition overlay
- **WHEN** the settings dialog is open and the user clicks and drags the visible overlay graphic
- **THEN** the overlay moves interactively to follow the cursor, and the new center coordinates are saved upon release

#### Scenario: Resize overlay maintaining aspect ratio
- **WHEN** the user drags a resize handle or edge on the WYSIWYG overlay
- **THEN** the overlay dimensions update while strictly preserving the image's original aspect ratio

#### Scenario: Settings dialog closes
- **WHEN** the settings dialog is closed
- **THEN** the overlay exits WYSIWYG interaction mode, returns to standard click-through behavior, and applies the current live state visibility

### Requirement: Server IP Discovery Dropdown with Offline Fallback
The settings dialog SHALL provide a dropdown list of server IP addresses detected broadcasting on the configured port, rendering an inaccessible saved IP with red strikethrough styling.

#### Scenario: Discovered servers displayed
- **WHEN** the client receives broadcast packets from one or more servers on the configured port
- **THEN** the dropdown lists the detected servers by IP and metadata

#### Scenario: Configured server IP offline
- **WHEN** the settings dialog opens and the server IP stored in settings is not detected on the network
- **THEN** the saved IP is displayed as the first selected item with red strikethrough styling followed by any active servers

#### Scenario: Inaccessible server IP remains in configuration
- **WHEN** the settings dialog is closed without selecting a different server IP
- **THEN** the stored IP address remains saved in `mic-helper-client-settings.json`

### Requirement: Animation Sliders Configuration
The settings dialog SHALL provide sliders for maximum opacity (range 0 to 100, default 100) and pulse frequency (range 0.1 to 5.0 seconds, default 1.0s).

#### Scenario: Adjust maximum opacity
- **WHEN** the user slides the opacity control
- **THEN** the overlay's peak pulse opacity updates immediately to the selected percentage

#### Scenario: Adjust pulse frequency
- **WHEN** the user slides the frequency control
- **THEN** the cycle period of the sine-wave pulse adjusts immediately to the selected duration in seconds

### Requirement: Local JSON Settings Persistence
The client application SHALL store all configuration settings in `mic-helper-client-settings.json` located strictly within the application's executable directory.

#### Scenario: Settings saved on modification
- **WHEN** any setting (port, server IP, retry timeout, opacity, frequency, overlay geometry) is modified
- **THEN** the changes are saved immediately to `mic-helper-client-settings.json` in the local application folder

#### Scenario: Isolated configuration for different directories
- **WHEN** two client executables run from different folders (e.g., `C:\Client1` and `C:\Client2`)
- **THEN** each client reads and writes exclusively to its own folder's `mic-helper-client-settings.json` without cross-instance interference

### Requirement: Path-Unique Windows Startup Registration
The client application SHALL support automatic startup on Windows login by creating an instance-unique entry in `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` based on the executable path.

#### Scenario: Enable run on startup
- **WHEN** the user enables "Run on startup" in the settings dialog
- **THEN** the application adds a registry value named uniquely using the application type and executable path hash, pointing to the exact executable path

#### Scenario: Disable run on startup
- **WHEN** the user disables "Run on startup"
- **THEN** the application removes its specific unique registry value without affecting startup entries belonging to instances in other folders

### Requirement: Per-Folder Single Instance Enforcement
The client application SHALL ensure only one instance runs from a specific directory while allowing concurrent instances from different directories.

#### Scenario: Duplicate launch in same directory
- **WHEN** a user attempts to launch a second client executable from the same directory
- **THEN** the second instance detects the existing instance via a path-scoped system mutex and exits without disrupting the running instance

#### Scenario: Concurrent launches in different directories
- **WHEN** a user launches a client executable from a different directory
- **THEN** the new instance acquires its own path-scoped mutex and starts successfully

### Requirement: Client Settings Application Version Display
The client settings dialog SHALL display the application version embedded during compilation, positioned at the bottom of the dialog between the "View Logs..." button and the "Close" button.

#### Scenario: Display current version in client settings
- **WHEN** the user opens the client settings dialog
- **THEN** the application version (e.g., `v0.1.0`) is displayed centered between the "View Logs..." and "Close" buttons, reflecting the version value defined at build time from the repository `VERSION` file.

