## MODIFIED Requirements

### Requirement: Default Device Fallback and Saved Selection Preservation
The server audio monitor SHALL resolve the system default capture endpoint by prioritizing the standard general/console default device (`ERole.eConsole`), falling back to the default communication endpoint (`ERole.eCommunications`) or multimedia endpoint (`ERole.eMultimedia`) only when a general console default is unavailable, and preserve a missing saved device in settings when it is temporarily unplugged.

#### Scenario: First application launch
- **WHEN** the server starts with no configured microphone in settings
- **THEN** the system resolves the Windows default console capture device (`ERole.eConsole`), binds to it, saves its identity in settings, and falls back to `ERole.eCommunications` or `ERole.eMultimedia` if console is unavailable

#### Scenario: Configured microphone not found on startup
- **WHEN** the server launches but the microphone specified in settings is not attached
- **THEN** the server retains the configured device identifier in settings, marks device state as disconnected, and does not overwrite the setting with an arbitrary device
