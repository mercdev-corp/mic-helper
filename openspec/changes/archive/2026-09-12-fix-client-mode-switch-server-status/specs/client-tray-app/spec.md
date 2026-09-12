## MODIFIED Requirements

### Requirement: Client Operational Mode Selection
The client application SHALL provide an option to switch between "Dual PC" and "Single PC" operational modes, applying the mode change immediately and restoring the active mode upon application startup.

#### Scenario: Switch from Dual PC to Single PC mode
- **WHEN** the user selects "Single PC" mode in the settings dialog
- **THEN** the client immediately transitions to Single PC mode, saves the active mode in settings, deactivates network UDP listening, and activates local audio capture monitoring using the saved microphone configuration

#### Scenario: Switch from Single PC to Dual PC mode
- **WHEN** the user selects "Dual PC" mode in the settings dialog
- **THEN** the client immediately transitions to Dual PC mode, saves the active mode in settings, stops local audio capture monitoring, activates UDP network listening using the saved server and port settings, and dynamically updates server status without requiring an application pause/resume cycle

#### Scenario: Startup restores active mode
- **WHEN** the client application launches
- **THEN** it loads the active mode from `mic-helper-client-settings.json` (defaulting to Dual PC mode if omitted) and activates only the subsystem for that mode
