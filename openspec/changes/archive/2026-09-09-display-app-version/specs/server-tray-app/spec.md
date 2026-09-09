## ADDED Requirements

### Requirement: Server Settings Application Version Display
The server settings dialog SHALL display the application version embedded during compilation, positioned at the bottom of the dialog between the "View Logs..." button and the "Close" button.

#### Scenario: Display current version in server settings
- **WHEN** the user opens the server settings dialog
- **THEN** the application version (e.g., `v0.1.0`) is displayed centered between the "View Logs..." and "Close" buttons, reflecting the version value defined at build time from the repository `VERSION` file.
