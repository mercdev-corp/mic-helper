## MODIFIED Requirements

### Requirement: Client System Tray Status Visualization
The client application SHALL display a system tray icon reflecting the current operational state: Paused, Disconnected (cannot reach server in Dual PC mode or missing local microphone in Single PC mode), Microphone Muted, or Microphone Unmuted.

#### Scenario: Display muted state in tray
- **WHEN** connected to the server (in Dual PC mode) or monitoring local microphone (in Single PC mode) and the microphone is muted
- **THEN** the system tray shows the microphone muted status icon

#### Scenario: Display server disconnected in tray
- **WHEN** in Dual PC mode and the target server is unreachable or timed out
- **THEN** the system tray shows the disconnected status icon

#### Scenario: Display local microphone disconnected in tray
- **WHEN** in Single PC mode and the selected microphone is disconnected or not found
- **THEN** the system tray shows the disconnected status icon

#### Scenario: Display client paused state in tray
- **WHEN** the user pauses the client application
- **THEN** the system tray shows the paused status icon

### Requirement: Local JSON Settings Persistence
The client application SHALL store all configuration settings in `mic-helper-client-settings.json` located strictly within the application's executable directory, preserving independent Dual PC and Single PC configuration parameters across mode switches.

#### Scenario: Settings saved on modification
- **WHEN** any setting (operational mode, port, server IP, microphone ID, microphone name, retry timeout, opacity, frequency, overlay geometry) is modified
- **THEN** the changes are saved immediately to `mic-helper-client-settings.json` in the local application folder

#### Scenario: Coexistence of Dual PC and Single PC parameters
- **WHEN** the user switches between Dual PC mode and Single PC mode
- **THEN** the previous settings for the alternate mode (server IP and port for Dual PC; microphone ID and name for Single PC) remain preserved in `mic-helper-client-settings.json` and are restored upon returning to that mode

#### Scenario: Isolated configuration for different directories
- **WHEN** two client executables run from different folders (e.g., `C:\Client1` and `C:\Client2`)
- **THEN** each client reads and writes exclusively to its own folder's `mic-helper-client-settings.json` without cross-instance interference

## ADDED Requirements

### Requirement: Client Operational Mode Selection
The client application SHALL provide an option to switch between "Dual PC" and "Single PC" operational modes, applying the mode change immediately and restoring the active mode upon application startup.

#### Scenario: Switch from Dual PC to Single PC mode
- **WHEN** the user selects "Single PC" mode in the settings dialog
- **THEN** the client immediately transitions to Single PC mode, saves the active mode in settings, deactivates network UDP listening, and activates local audio capture monitoring using the saved microphone configuration

#### Scenario: Switch from Single PC to Dual PC mode
- **WHEN** the user selects "Dual PC" mode in the settings dialog
- **THEN** the client immediately transitions to Dual PC mode, saves the active mode in settings, stops local audio capture monitoring, and activates UDP network listening using the saved server and port settings

#### Scenario: Startup restores active mode
- **WHEN** the client application launches
- **THEN** it loads the active mode from `mic-helper-client-settings.json` (defaulting to Dual PC mode if omitted) and activates only the subsystem for that mode

### Requirement: Adaptive Settings Dialog Layout
The client settings dialog SHALL adapt its layout based on the active operational mode, rendering Dual PC controls or Single PC controls while preserving shared overlay and application configuration.

#### Scenario: Dual PC layout presentation
- **WHEN** the settings dialog is viewed in Dual PC mode
- **THEN** the dialog displays the note "Run server app on remote PC where your Microphone is plugged in", displays the server IP dropdown and UDP port field, and hides the microphone selection dropdown

#### Scenario: Single PC layout presentation
- **WHEN** the settings dialog is viewed in Single PC mode
- **THEN** the dialog displays the shared microphone selection dropdown, hides the server IP dropdown, UDP port field, and Dual PC note, and labels retry timeout for microphone reconnection checks

#### Scenario: Single PC missing microphone styling
- **WHEN** the settings dialog is in Single PC mode and the saved microphone is not currently connected to the machine
- **THEN** the microphone dropdown renders the saved microphone as the first selected item with red strikethrough styling, followed by all currently connected capture devices

#### Scenario: Shared controls accessibility across modes
- **WHEN** switching between modes in the settings dialog
- **THEN** Run on startup, Enable debug logging, Retry timeout, Overlay maximum opacity, Pulse frequency, Overlay size, View Logs, and version display remain accessible and retain their values

### Requirement: Mode Execution Resource Isolation
The client application SHALL completely deactivate the unused mode's subsystems to avoid unnecessary CPU, audio callback, or network resource consumption.

#### Scenario: Dual PC mode halts audio monitoring
- **WHEN** the client is running in Dual PC mode
- **THEN** the client does not initialize audio capture endpoints, register audio device notifications, or execute microphone reconnection polling loops

#### Scenario: Single PC mode halts network listener
- **WHEN** the client is running in Single PC mode
- **THEN** the client does not bind to UDP network ports, listen for network broadcasts, or process incoming UDP packets
