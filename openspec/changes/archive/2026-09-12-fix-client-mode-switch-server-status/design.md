## Context

In `src/MicHelper.Client/UI/ClientSettingsForm.cs`, when the user selects a new mode from `_cboMode`, `CboMode_SelectedIndexChanged` directly mutates the shared `_settings.Mode` property before invoking `_onModeChanged?.Invoke(selectedMode)`.

In `src/MicHelper.Client/ClientTrayApplicationContext.cs`, `SwitchMode(ClientMode newMode)` begins with:
```csharp
if (_settings.Mode == newMode) return;
```
Because `_settings` is shared between `ClientSettingsForm` and `ClientTrayApplicationContext`, `_settings.Mode` has already been updated to `newMode` by the form. The guard condition immediately evaluates to true and exits without performing any runtime subsystem transitions:
- `_audioMonitor.StopMonitoring()` is skipped.
- `_udpListener.Start(...)` is skipped.
- Overlay live state and tray icon updates are skipped.

Additionally, `UdpListener.Stop()` does not reset `_isConnected` to false or `_lastReportedState` to `MicState.Disconnected`. If the client was previously connected, `_isConnected` remains true during Single PC mode, and returning to Dual PC mode suppresses `TargetServerConnectionChanged` events because `_isConnected` does not transition from false to true.

Manual pause and resume via the tray menu (`OnPauseResumeClicked`) works around this because it does not have the `_settings.Mode == newMode` early-return check and unconditionally starts `_udpListener.Start(...)` when resumed in Dual PC mode.

## Goals / Non-Goals

**Goals:**
- Ensure that selecting "Dual PC" mode in `ClientSettingsForm` immediately activates UDP network listening and deactivates local audio monitoring.
- Ensure that selecting "Single PC" mode in `ClientSettingsForm` immediately activates local audio monitoring and halts UDP network listening.
- Decouple subsystem switching in `ClientTrayApplicationContext` from pre-mutated `_settings.Mode` by tracking active runtime mode or verifying subsystem state.
- Ensure `UdpListener.Stop()` properly cleans up connection state and dispatches disconnect notifications if previously connected.
- Ensure `ClientSettingsForm` invokes mode transition callbacks before populating mode-specific controls and refreshes server status upon receiving broadcast packets.

**Non-Goals:**
- Modifying the UDP packet protocol or multicast broadcast address.
- Changing microphone enumeration or selection logic for Single PC mode.
- Changing client settings persistence schema or configuration keys.

## Decisions

### Decision 1: Track Active Runtime Mode in `ClientTrayApplicationContext`
- **Approach**: Maintain a private field `ClientMode _activeMode` in `ClientTrayApplicationContext` initialized from `_settings.Mode` at startup. In `SwitchMode(ClientMode newMode)`, compare `_activeMode == newMode` (and ensure subsystems match) rather than `_settings.Mode == newMode`. Update `_activeMode = newMode; _settings.Mode = newMode; _settings.Save();`.
- **Rationale**: `_settings` is passed by reference to `ClientSettingsForm`. If callers or UI controls mutate `_settings.Mode` prior to invoking the callback, `_activeMode` accurately reflects what subsystems are currently running and guarantees that the transition logic is never skipped.
- **Alternatives considered**:
  - *Remove early return entirely*: Works, but tracking `_activeMode` prevents redundant subsystem restarts when the user clicks or re-selects the same mode.
  - *Prevent `ClientSettingsForm` from mutating `_settings.Mode`*: Clean, but fragile if another caller or UI binding sets `_settings.Mode`. Tracking `_activeMode` in context provides defense-in-depth.

### Decision 2: Order of Execution in `ClientSettingsForm.CboMode_SelectedIndexChanged`
- **Approach**:
  1. Notify `_onModeChanged?.Invoke(selectedMode)` so the background networking or audio subsystem starts immediately.
  2. Apply layout adjustments via `ApplyLayoutForMode(selectedMode)`.
  3. Populate mode-specific controls (`PopulateServerIpList()` or `_micController.Populate(...)`).
  4. Ensure `TargetServerConnectionChanged` and `DiscoveredServersUpdated` events continue to automatically update `_cboServerIp` as packets arrive from the target server.
- **Rationale**: Triggering `_onModeChanged` first initiates socket binding and packet reception without delay, allowing server discovery and connection events to update the UI promptly.
- **Alternatives considered**:
  - *Keep `PopulateServerIpList()` before `_onModeChanged`*: Always renders the server as offline on initial draw until the first packet arrives, causing unnecessary visual flashing.

### Decision 3: Clean State Reset in `UdpListener.Stop()`
- **Approach**: In `UdpListener.Stop()`, call `SetConnected(false)` and `SetState(MicState.Disconnected)`. In `Start()`, reset `_lastTargetPacketTime = DateTime.MinValue`.
- **Rationale**: When the listener is stopped, it is not connected. Explicitly updating `_isConnected` to false ensures that when `Start()` is subsequently called and the first packet arrives, `SetConnected(true)` correctly observes `_isConnected != true` and fires `TargetServerConnectionChanged(true)`.
- **Alternatives considered**:
  - *Leave state unchanged on `Stop()`*: Leaves stale state and prevents event listeners in `ClientSettingsForm` and `ClientTrayApplicationContext` from reacting to reconnection.

## Risks / Trade-offs

- **[Risk]** Packet arrival latency after switching to Dual PC mode (up to 1.0s heartbeat delay) might leave the server temporarily marked offline if the discovered server list was pruned.
  - **Mitigation**: `PopulateServerIpList()` checks both active connection and recent discovered servers. When the listener receives the server's heartbeat datagram, `DiscoveredServersUpdated` and `TargetServerConnectionChanged` immediately re-render `_cboServerIp` without requiring user interaction.
- **[Risk]** Thread-safety when switching modes rapidly.
  - **Mitigation**: `SwitchMode` marshals execution to the UI thread via `PostToUiThread`, and `UdpListener` uses internal locks and thread-safe cancellation tokens for socket lifecycle operations.
