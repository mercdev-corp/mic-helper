## Purpose

Specifies the anti-cheat-safe, ultra-low-overhead topmost click-through overlay window, pulsing opacity animation engine, and pre-rendered asset caching mechanism for displaying microphone and server statuses.

## ADDED Requirements

### Requirement: Anti-Cheat Safe Click-Through Overlay Window
The client overlay SHALL be implemented as an external desktop layered window with `WS_EX_TOPMOST`, `WS_EX_TRANSPARENT`, `WS_EX_LAYERED`, `WS_EX_NOACTIVATE`, and `WS_EX_TOOLWINDOW` styles, without hooking game APIs or inspecting foreign processes.

#### Scenario: User clicks game screen beneath overlay
- **WHEN** the overlay is visible on top of an active borderless or fullscreen game window and the user clicks over the overlay region
- **THEN** all mouse and keyboard inputs pass directly through to the underlying application or game without interception or latency

#### Scenario: Overlay displays over fullscreen applications
- **WHEN** a game or application runs in fullscreen or borderless windowed mode
- **THEN** the overlay remains rendered on top of the display without stealing focus or activating a window title bar

### Requirement: State-Driven Overlay Visibility
The client overlay SHALL display the corresponding status visual only during active alert conditions (muted or disconnected) and remain completely hidden during normal or paused operations.

#### Scenario: Microphone is muted
- **WHEN** the client receives a Muted status from the server
- **THEN** the overlay displays the pulsing "microphone muted" graphic at the configured screen position

#### Scenario: Server is disconnected
- **WHEN** the server connection is lost or timed out
- **THEN** the overlay displays the pulsing "server disconnected" graphic at the configured screen position

#### Scenario: Microphone is unmuted or client is paused
- **WHEN** the microphone is unmuted or the client application is in a paused state
- **THEN** the overlay is completely hidden from view and its render loop is suspended

### Requirement: Sine-Wave Pulsing Opacity and Zero-CPU Idle
The client overlay engine SHALL calculate transparency using a smooth sine-wave function between 0 and the configured maximum opacity, and SHALL halt animation timers completely when the overlay is hidden.

#### Scenario: Pulsing active overlay
- **WHEN** the overlay is visible in an alert state (muted or disconnected)
- **THEN** the opacity smoothly oscillates from 0 to the configured maximum opacity (0-100%) according to the configured pulse period (0.1 to 5.0 seconds)

#### Scenario: Zero CPU consumption when hidden
- **WHEN** the overlay transitions to hidden
- **THEN** the animation timer is disabled and no render updates or CPU ticks occur for overlay drawing

### Requirement: Pre-Rendered Asset Resizing and Caching
The client overlay engine SHALL pre-render and cache scaled bitmap images upon configuration save to eliminate real-time image scaling and GPU/CPU interpolation overhead during runtime playback.

#### Scenario: Settings dialog closes after overlay resize
- **WHEN** the user resizes the overlay in the settings dialog and closes settings
- **THEN** the overlay engine renders the resized images at the exact pixel dimensions, caches them to disk/memory in the application folder, and utilizes the pre-rendered bitmaps for subsequent overlay display

### Requirement: Independent Overlay Positioning per Instance
The client overlay engine SHALL permit multiple client instances running from different folders to display separate overlays at independent screen coordinates simultaneously.

#### Scenario: Concurrent client overlays on screen
- **WHEN** two client instances run from different directories tracking distinct server ports
- **THEN** each client renders its own independent overlay at its own configured screen coordinates without visual collision or coordinate overwrites
