## 1. Shared Microphone UI & Refactoring

- [x] 1.1 Create `MicComboItem` and `MicrophoneSelectionController` in `src/MicHelper.Shared/UI` encapsulating active device querying via `IAudioMonitor`, combobox population, missing device handling, and owner-draw rendering (red strikethrough styling for missing items); verify by compiling `MicHelper.Shared`.
- [x] 1.2 Refactor `src/MicHelper.Server/UI/ServerSettingsForm.cs` to consume `MicrophoneSelectionController` instead of duplicate combobox population and drawing code; verify by running existing server tests via `dotnet test --filter FullyQualifiedName~Server`.

## 2. Client Settings Data Model

- [x] 2.1 Add `ClientMode` enum (`DualPc`, `SinglePc`) and `Mode`, `MicrophoneId`, and `MicrophoneName` properties to `ClientSettings` and its JSON serialization context in `src/MicHelper.Client/Config/ClientSettings.cs`, defaulting `Mode` to `ClientMode.DualPc`; verify by running serialization unit tests.
- [x] 2.2 Add unit tests in `tests/MicHelper.Tests` verifying settings coexistence (ensuring Dual PC `ServerIp`/`Port` and Single PC `MicrophoneId`/`MicrophoneName` survive cross-mode modifications); verify by executing `dotnet test --filter FullyQualifiedName~Client`.

## 3. Client Subsystem Lifecycle and Resource Isolation

- [x] 3.1 Update `ClientTrayApplicationContext` in `src/MicHelper.Client/ClientTrayApplicationContext.cs` to hold both `UdpListener` and `IAudioMonitor`, initializing only the active mode's subsystem on application start; verify via mode startup unit tests.
- [x] 3.2 Implement live mode switching in `ClientTrayApplicationContext`: when switching to Single PC mode, stop `UdpListener` and start `IAudioMonitor`; when switching to Dual PC mode, stop `IAudioMonitor` and start `UdpListener`; wire audio events (`MuteChanged`, `ConnectionChanged`) to overlay and tray icon updates; verify resource isolation and thread marshaling via tests.

## 4. Client Settings Dialog UI

- [x] 4.1 Add mode selection control and Dual PC guidance note ("Run server app on remote PC where your Microphone is plugged in") to `ClientSettingsForm` in `src/MicHelper.Client/UI/ClientSettingsForm.cs`.
- [x] 4.2 Add the microphone selection dropdown to `ClientSettingsForm` powered by `MicrophoneSelectionController` in `src/MicHelper.Shared/UI`.
- [x] 4.3 Implement dynamic layout adjustment in `ClientSettingsForm`: toggle visibility between Dual PC controls (guidance note, server dropdown, port input) and Single PC controls (microphone dropdown), re-label retry timeout for microphone reconnection checks, and adjust control vertical positioning cleanly.
- [x] 4.4 Wire mode selection and microphone changes to update settings and notify `ClientTrayApplicationContext` immediately; verify by testing settings save on UI interactions.

## 5. End-to-End Verification

- [x] 5.1 Add comprehensive automated tests in `tests/MicHelper.Tests` validating mode persistence, layout state transitions, and resource isolation (ensuring UDP listener is stopped in Single PC mode and audio monitor is stopped in Dual PC mode); verify by running `dotnet test`.
- [x] 5.2 Build the complete solution across all projects in Release configuration (`dotnet build -c Release MicHelper.sln`) and run all automated test suites to confirm zero regressions.
