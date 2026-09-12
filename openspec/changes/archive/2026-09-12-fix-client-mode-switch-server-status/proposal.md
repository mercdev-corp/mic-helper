## Why

When switching the client application from Single PC mode to Dual PC mode, the target server is marked as offline in the settings server list and the client fails to receive server status updates until the user manually pauses and resumes the client app via the tray menu.

This occurs because `ClientSettingsForm` directly mutates `_settings.Mode` before triggering `_onModeChanged`, causing `ClientTrayApplicationContext.SwitchMode` to evaluate `_settings.Mode == newMode` as true and immediately return without starting `UdpListener` or stopping `IAudioMonitor`. Furthermore, `UdpListener.Stop()` fails to reset its internal connection state, causing stale connection flags to suppress subsequent connection event dispatches. Fixing this ensures seamless mode transitions and immediate, accurate server discovery upon returning to Dual PC mode.

## What Changes

- **Synchronized Mode Switching**: Refactor mode switching coordination between `ClientSettingsForm` and `ClientTrayApplicationContext` so that switching between Single PC and Dual PC modes always activates the target subsystem and deactivates the unused subsystem regardless of prior setting mutations.
- **UdpListener State Reset**: Ensure `UdpListener.Stop()` explicitly sets `IsConnected` to false and resets the last reported state to `MicState.Disconnected`, guaranteeing that re-starting the listener correctly detects and announces target server connections.
- **Immediate Server List Refresh**: Ensure `ClientSettingsForm` initiates subsystem transitions before populating server items, and re-renders the server dropdown when the UDP listener receives broadcasts and connection status changes.

## Capabilities

### New Capabilities
<!-- None -->

### Modified Capabilities
- `client-tray-app`: Clarify that switching from Single PC to Dual PC mode immediately activates UDP network listening, halts local audio monitoring, and reflects server availability in the settings dropdown without requiring application pause/resume.
- `core-network-protocol`: Specify that stopping the UDP listener immediately clears active connection state so subsequent starts accurately detect server reconnections.

## Impact

- **Code Projects**:
  - `src/MicHelper.Client/UI/ClientSettingsForm.cs`: Refactor `CboMode_SelectedIndexChanged` to notify mode change listeners properly and coordinate UI updates.
  - `src/MicHelper.Client/ClientTrayApplicationContext.cs`: Ensure `SwitchMode` performs runtime subsystem transitions without being blocked by premature `_settings.Mode` mutations.
  - `src/MicHelper.Shared/Network/UdpListener.cs`: Reset connection state in `Stop()`.
- **Tests**: Add unit tests in `MicHelper.Tests` verifying mode transitions between Single PC and Dual PC triggered from `ClientSettingsForm`, ensuring listener activation, audio monitor shutdown, and accurate server online/offline representation.
- **Backward Compatibility**: Fully backward compatible. No changes to configuration schema or network protocol.
