## Why

Users who stream and play on a single PC currently must run both the Server application (to monitor microphone hardware) and the Client application (to render the overlay graphic). Adding a selectable "Single PC" mode to the Client application eliminates the need to run the Server application separately for single-PC setups, while refactoring microphone selection and mute detection into shared components ensures consistent behavior across both applications without unnecessary background overhead.

## What Changes

- **Shared Audio UI & Logic**: Extract microphone enumeration, dropdown population, missing device indication (red strikethrough), and mute/connection event tracking into reusable shared components in `MicHelper.Shared` used by both Server and Client.
- **Client Mode Selection**: Add an immediate-switching mode selector ("Dual PC" vs "Single PC") to the Client settings dialog, defaulted to Dual PC (or previously saved mode) and persisted in `mic-helper-client-settings.json`.
- **Dual PC Mode Behavior**: Maintain existing UDP network broadcast listening, hide microphone selection controls, display a clarifying note ("Run server app on remote PC where your Microphone is plugged in"), and completely deactivate local audio device polling and event callbacks.
- **Single PC Mode Behavior**: Dynamically adjust the Client settings dialog by replacing the server dropdown with the shared microphone selection dropdown, hiding the UDP port input, and repurposing "Retry timeout (seconds)" for microphone reconnection checks. Deactivate UDP network listening and packet processing completely while directly monitoring local Windows Core Audio capture events.
- **Configuration Coexistence**: Ensure Dual PC settings (`server_ip`, `port`) and Single PC settings (`microphone_id`, `microphone_name`, retry timeout) coexist in `mic-helper-client-settings.json` so users can switch between modes without losing previously entered values. Overlay visual settings remain common across both modes.

## Capabilities

### New Capabilities
<!-- None -->

### Modified Capabilities
- `client-tray-app`: Add requirements for mode switching (Dual PC vs Single PC), dynamic settings dialog layout based on active mode, mode-specific logic isolation (no audio monitoring in Dual PC; no network listening in Single PC), and coexisting settings persistence.

## Impact

- **Code Projects**:
  - `src/MicHelper.Shared`: Introduce shared microphone selection dropdown helper/controller and common audio monitoring state coordination.
  - `src/MicHelper.Server`: Refactor `ServerSettingsForm` and `ServerTrayApplicationContext` to consume shared microphone selection and audio monitoring components.
  - `src/MicHelper.Client`: Update `ClientSettings`, `ClientTrayApplicationContext`, and `ClientSettingsForm` to support Dual PC / Single PC modes, dynamic UI adjustment, and strict mode isolation.
- **Settings Schema**: `mic-helper-client-settings.json` adds `mode` property (e.g., `"DualPc"` / `"SinglePc"`) and microphone properties (`microphone_id`, `microphone_name`).
- **Performance**: Zero idle CPU/network overhead from the unused mode (audio callbacks stopped in Dual PC; UDP sockets/listener closed in Single PC).
- **Backward Compatibility**: Fully backward compatible. Existing client configuration files missing the `mode` property default to `DualPc` mode.
