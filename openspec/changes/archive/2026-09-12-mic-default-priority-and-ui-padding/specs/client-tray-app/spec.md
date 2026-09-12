## ADDED Requirements

### Requirement: Settings Dialog Layout and Right Padding Guarantees
The client settings dialog SHALL maintain a consistent minimum right margin of 20 pixels for all labels, hints, and controls, wrapping informational text and multi-line notes so no text clips or touches the right window boundary across standard and high-DPI scaling.

#### Scenario: Dual PC guidance note padding
- **WHEN** the settings dialog displays the Dual PC guidance note ("Run server app on remote PC where your Microphone is plugged in")
- **THEN** the text wraps within the dialog layout boundaries and maintains at least 20 pixels of padding from the right edge of the client area

#### Scenario: Interaction hint label padding
- **WHEN** the settings dialog displays the overlay interaction hint ("Drag overlay to move • Scroll mouse wheel or drag corners to resize.")
- **THEN** the hint text remains fully visible with at least 20 pixels of clearance from the right window border without truncation or clipping

## MODIFIED Requirements

### Requirement: Adaptive Settings Dialog Layout
The client settings dialog SHALL adapt its layout based on the active operational mode, rendering Dual PC controls or Single PC controls while preserving shared overlay and application configuration, and clearly labeling default audio capture endpoints.

#### Scenario: Dual PC layout presentation
- **WHEN** the settings dialog is viewed in Dual PC mode
- **THEN** the dialog displays the note "Run server app on remote PC where your Microphone is plugged in", displays the server IP dropdown and UDP port field, and hides the microphone selection dropdown

#### Scenario: Single PC layout presentation
- **WHEN** the settings dialog is viewed in Single PC mode
- **THEN** the dialog displays the shared microphone selection dropdown, hides the server IP dropdown, UDP port field, and Dual PC note, labels retry timeout for microphone reconnection checks, and indicates default devices with distinct `(Default)` and `(Default Communications)` badges when endpoints differ

#### Scenario: Single PC missing microphone styling
- **WHEN** the settings dialog is in Single PC mode and the saved microphone is not currently connected to the machine
- **THEN** the microphone dropdown renders the saved microphone as the first selected item with red strikethrough styling, followed by all currently connected capture devices

#### Scenario: Shared controls accessibility across modes
- **WHEN** switching between modes in the settings dialog
- **THEN** Run on startup, Enable debug logging, Retry timeout, Overlay maximum opacity, Pulse frequency, Overlay size, View Logs, and version display remain accessible and retain their values
