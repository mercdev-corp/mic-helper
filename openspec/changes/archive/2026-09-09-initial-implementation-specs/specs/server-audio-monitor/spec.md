## Purpose

Specifies the Windows Core Audio subsystem integration on the server host, delivering event-driven microphone mute detection, hardware plug/unplug notification, and isolated per-instance device targeting.

## ADDED Requirements

### Requirement: Windows Core Audio Device Enumeration
The server audio monitor SHALL enumerate all active audio capture endpoints on the system and expose their system identifiers and human-readable device names.

#### Scenario: Enumerate active capture devices
- **WHEN** the audio subsystem queries system capture devices
- **THEN** it retrieves all active microphones with their unique endpoint ID strings and friendly names

### Requirement: Event-Driven Zero-Polling Mute Detection
The server audio monitor SHALL register for native volume and mute event notifications on the selected audio endpoint to detect state changes instantaneously without continuous CPU polling.

#### Scenario: Hardware or software mute toggle
- **WHEN** the user mutes or unmutes the selected microphone via hardware switch, Windows volume mixer, or hotkey
- **THEN** the system receives an immediate audio notification callback and notifies the server state engine without consuming idle CPU cycles

### Requirement: Device Hotplug and Unplug Tracking
The server audio monitor SHALL monitor device state notifications to detect when the selected microphone is removed or reattached.

#### Scenario: Monitored microphone is disconnected
- **WHEN** the physical microphone is unplugged from the host machine
- **THEN** the audio monitor marks the device as unavailable, transitions state to disconnected, and activates a periodic probe loop based on the configured retry timeout

#### Scenario: Monitored microphone is reconnected
- **WHEN** the selected microphone is re-plugged into the host machine
- **THEN** the audio monitor re-establishes volume change callbacks, reads current mute status, and resumes active status reporting

### Requirement: Default Device Fallback and Saved Selection Preservation
The server audio monitor SHALL default to the system default communication/capture device when no configuration exists, and preserve a missing saved device in settings when it is temporarily unplugged.

#### Scenario: First application launch
- **WHEN** the server starts with no configured microphone in settings
- **THEN** the system resolves the current Windows default capture device, binds to it, and saves its identity in settings

#### Scenario: Configured microphone not found on startup
- **WHEN** the server launches but the microphone specified in settings is not attached
- **THEN** the server retains the configured device identifier in settings, marks device state as disconnected, and does not overwrite the setting with an arbitrary device

### Requirement: Independent Device Targeting per Instance
The server audio monitor SHALL allow independent instances of the application running from different directories to bind and monitor distinct microphone endpoints simultaneously.

#### Scenario: Multiple servers monitoring different microphones
- **WHEN** two server instances run from different directories on the same host
- **THEN** each instance binds to its own configured microphone without interfering with the other instance's event callbacks
