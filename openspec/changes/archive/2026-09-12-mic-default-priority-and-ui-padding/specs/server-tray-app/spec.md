## ADDED Requirements

### Requirement: Server Settings Dialog Layout and Margin Guarantees
The server settings dialog SHALL maintain a consistent minimum right margin of 20 pixels for all controls, labels, and text elements, preventing any text or input controls from clipping or extending flush against the right window boundary across standard and high-DPI scaling.

#### Scenario: Server settings controls layout padding
- **WHEN** the server settings dialog is opened
- **THEN** all controls, buttons, and descriptive labels maintain at least 20 pixels of clearance from the right edge of the window

## MODIFIED Requirements

### Requirement: Microphone Selection with Missing Device Indication
The settings dialog SHALL provide a dropdown list of available microphones, defaulting to the system default on first run, rendering distinct badges for general default and communications default devices when they differ, and rendering a missing configured microphone in red strikethrough text.

#### Scenario: Configured microphone missing in dropdown
- **WHEN** the settings dialog opens and the currently saved microphone is not present on the host
- **THEN** the missing microphone is displayed as the first item with red strikethrough styling, followed by all currently connected microphones

#### Scenario: Missing microphone persists unless changed
- **WHEN** the settings dialog is closed without selecting a different microphone
- **THEN** the missing microphone remains preserved in `mic-helper-server-settings.json`

#### Scenario: Distinct default device indication
- **WHEN** the system has distinct default console and communications capture devices
- **THEN** the dropdown indicates the primary console default microphone with `(Default)` and the communications default microphone with `(Default Communications)`
