## 1. Network Subsystem State Reset

- [x] 1.1 In `src/MicHelper.Shared/Network/UdpListener.cs`, update `Stop()` to reset `_isConnected = false` and `_lastReportedState = MicState.Disconnected`, notifying `TargetServerConnectionChanged` if previously connected, and verify via unit test.
- [x] 1.2 In `src/MicHelper.Shared/Network/UdpListener.cs`, ensure `Start()` resets `_lastTargetPacketTime` to `DateTime.MinValue` and maintains consistent connection state upon restart.

## 2. Client Mode Switching Synchronization

- [x] 2.1 In `src/MicHelper.Client/ClientTrayApplicationContext.cs`, maintain an internal `_activeMode` state and update `SwitchMode(ClientMode newMode)` so that subsystem activation and deactivation (`IAudioMonitor` vs `UdpListener`) execute reliably regardless of whether `_settings.Mode` was pre-updated by UI callers.
- [x] 2.2 In `src/MicHelper.Client/UI/ClientSettingsForm.cs`, refactor `CboMode_SelectedIndexChanged` so that `_onModeChanged?.Invoke(selectedMode)` is executed prior to `PopulateServerIpList()` and layout updates, ensuring the UDP listener is immediately active when entering Dual PC mode.

## 3. Automated Tests and Verification

- [x] 3.1 In `tests/MicHelper.Tests/ClientOverlayAndSettingsTests.cs`, add unit test verifying that switching from Single PC mode to Dual PC mode via `ClientSettingsForm` activates the UDP listener and deactivates the audio monitor without pause/resume.
- [x] 3.2 In `tests/MicHelper.Tests/ClientOverlayAndSettingsTests.cs`, add unit test verifying that `UdpListener.Stop()` clears `IsConnected` and dispatches disconnect notification.
- [x] 3.3 Run solution unit tests and verify all tests pass.
