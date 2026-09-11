## Context

Currently, the solution is partitioned into:
- `MicHelper.Server`: Monitors local Windows Core Audio capture endpoints for mute/unmute and hotplug events, and broadcasts state packets via UDP.
- `MicHelper.Client`: Receives UDP broadcasts, updates system tray status, and renders a DirectX/GDI+ overlay graphic.
- `MicHelper.Shared`: Houses shared protocol, UDP transport, audio capture interop (`IAudioMonitor`, `WindowsAudioMonitor`), and utility classes.

For streamers running both games and broadcasting software on a single PC, running two separate processes is redundant. By integrating microphone monitoring directly into `MicHelper.Client` as a selectable "Single PC" mode, single-PC users can run only the Client application.

See `proposal.md` for user motivation and `specs/client-tray-app/spec.md` for behavior requirements.

## Goals / Non-Goals

**Goals:**
- Provide a clear, immediate mode toggle between "Dual PC" and "Single PC" in the Client settings dialog.
- Move microphone selection, missing device indication (owner-drawn red strikethrough), and device change handling to reusable shared code in `MicHelper.Shared.UI`.
- Refactor `ServerSettingsForm` to use the shared microphone UI helper, ensuring identical behavior across Server and Client.
- Ensure strict runtime isolation: Dual PC mode consumes zero audio device resources; Single PC mode closes and halts all UDP network listening.
- Allow Dual PC configuration (server IP, port) and Single PC configuration (microphone ID, microphone name) to coexist in `mic-helper-client-settings.json` without overwriting each other.
- Dynamically adapt Client settings dialog UI layout depending on the selected mode.

**Non-Goals:**
- Merging Server and Client into a single combined executable binary. Server remains an independent lightweight executable for dedicated audio/mic streaming machines.
- Removing or altering the UDP broadcast protocol.
- Modifying the overlay rendering engine or animation logic.

## Decisions

### Decision 1: Shared Microphone UI Component (`MicrophoneSelectionController` in `MicHelper.Shared.UI`)
- **Choice**: Extract `MicComboItem`, dropdown population, missing device handling, and custom drawing (`DrawItem`) from `ServerSettingsForm` into a reusable `MicrophoneSelectionController` (or `MicrophoneComboBoxHelper`) in `MicHelper.Shared.UI`.
- **Rationale**: Keeps `ServerSettingsForm` and `ClientSettingsForm` DRY and guarantees identical user experience, missing device strikethrough styling, and device persistence rules.
- **Alternatives considered**:
  - *Duplicate combobox code in Client*: High risk of UI or behavior divergence between Server and Client over time.
  - *Create a UserControl*: WinForms custom UserControls can introduce designer serialization friction; a lightweight controller/helper that attaches to a standard `ComboBox` is cleaner and easier to test.

### Decision 2: Mode Representation and Settings Coexistence
- **Choice**: Add `ClientMode` enum (`DualPc`, `SinglePc`) and nullable `MicrophoneId` and `MicrophoneName` properties to `ClientSettings`.
- **Rationale**:
  - Setting `Mode` defaults to `DualPc` if absent from existing JSON files, preserving backward compatibility.
  - When in Single PC mode, `ServerIp` and `Port` remain untouched in JSON.
  - When in Dual PC mode, `MicrophoneId` and `MicrophoneName` remain untouched in JSON.
  - The existing `RetryTimeout` integer property is shared: in Dual PC mode it governs server packet timeout; in Single PC mode it governs microphone reconnect polling.
- **Alternatives considered**:
  - *Separate config files for each mode*: Creates file clutter and confuses users.
  - *Separate timeout properties (`ServerRetryTimeout` and `MicRetryTimeout`)*: Unnecessary complexity since users conceptualize both as "retry timeout".

### Decision 3: Subsystem Switching and Resource Isolation in `ClientTrayApplicationContext`
- **Choice**:
  - In `DualPc` mode: `UdpListener` is active; `IAudioMonitor` is stopped/inactive.
  - In `SinglePc` mode: `IAudioMonitor` is active (`StartMonitoring(...)`); `UdpListener` is stopped and its UDP socket unbound.
  - Switching modes immediately stops the previous subsystem, starts the target subsystem, and updates the tray icon and overlay live state.
- **Rationale**: Guarantees zero audio callback / CPU polling overhead in Dual PC mode, and zero UDP socket / network listening overhead in Single PC mode.
- **Alternatives considered**:
  - *Running a local in-process UDP server broadcast loop*: Incurs unnecessary socket I/O, potential firewall prompts, and port collisions. Direct local event consumption is faster and uses fewer resources.

### Decision 4: Adaptive Client Settings Form Layout
- **Choice**: Add a Mode ComboBox (`_cboMode`) at the top of `ClientSettingsForm`. Maintain controls for both modes, toggling visibility and adjusting control Y-coordinates dynamically:
  - **Dual PC mode**: Shows Dual PC note ("Run server app on remote PC where your Microphone is plugged in"), Server selection dropdown, Port field. Hides Microphone dropdown.
  - **Single PC mode**: Shows Microphone dropdown. Hides Dual PC note, Server dropdown, and Port field.
  - Common controls (Startup, Debug Logging, Retry Timeout, Opacity, Pulse Frequency, Overlay Size, View Logs, Version, Close) remain visible and smoothly repositioned.
- **Rationale**: Prevents empty vertical spaces when port and server fields are hidden, keeping the dialog compact and aesthetically pleasing.

## Risks / Trade-offs

- **[Risk] Audio callback execution on non-UI thread**: Core Audio COM notifications arrive on background threads.
  - **Mitigation**: Route all audio monitor event handlers through `_syncContext.Post(...)` in `ClientTrayApplicationContext` to ensure thread-safe updates to overlay and tray icon.
- **[Risk] Fast toggling between modes**: Rapidly switching back and forth between Dual PC and Single PC could cause race conditions in starting/stopping listeners or monitors.
  - **Mitigation**: Guard subsystem transitions sequentially with proper stop-before-start lifecycle calls on the UI thread.
- **[Risk] Form height or resize glitches when hiding controls**:
  - **Mitigation**: Use fixed dialog dimensions or deterministic coordinate adjustment so dialog size remains consistent and controls do not clip.
