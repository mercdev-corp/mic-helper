## 1. Core Audio Default Device Priority

- [x] 1.1 Update `GetDefaultCaptureDevice()` in `src/MicHelper.Shared/Audio/WindowsAudioMonitor.cs` to query `ERole.eConsole` first, then fall back to `ERole.eCommunications`, then `ERole.eMultimedia`; verify by compiling `MicHelper.Shared`.
- [x] 1.2 Update `AudioDeviceInfo` and `GetActiveCaptureDevices()` in `WindowsAudioMonitor.cs` to track both default console and default communications device identities; verify by running audio monitor unit tests.

## 2. Dropdown UI Default Badging

- [x] 2.1 Update `MicrophoneSelectionController.Populate` in `src/MicHelper.Shared/UI/MicrophoneSelectionController.cs` to append `(Default)` for the primary console device and `(Default Communications)` when the communications default is a distinct endpoint; verify by running unit tests for `MicrophoneSelectionController`.
- [x] 2.2 Add unit tests in `tests/MicHelper.Tests/AudioMonitorTests.cs` and `tests/MicHelper.Tests/ServerNetworkingAndSettingsTests.cs` validating `eConsole` priority and distinct default role badging; verify tests pass via `dotnet test`.

## 3. Settings Dialog Right Padding and Text Wrapping

- [x] 3.1 Update `src/MicHelper.Client/UI/ClientSettingsForm.cs` layout: expand `ClientSize.Width` to 400, configure `_lblDualPcNote` and `lblHint` with `MaximumSize = new Size(360, 0)` and `AutoSize = true`, expand full-width controls (comboboxes, trackbars) to 360px with `Location.X = 20`, and reposition bottom buttons to maintain at least 20px right margin; verify by checking control layout bounds.
- [x] 3.2 Update `src/MicHelper.Server/UI/ServerSettingsForm.cs` layout: expand `ClientSize.Width` to 400, expand full-width controls to 360px with `Location.X = 20`, and reposition bottom buttons to maintain at least 20px right margin; verify by checking control layout bounds.

## 4. Verification and Regression Testing

- [x] 4.1 Run the full test suite (`dotnet test tests/MicHelper.Tests/MicHelper.Tests.csproj -m:1 -nodeReuse:false`) to verify all audio monitor, controller, and form tests pass.
- [x] 4.2 Verify client and server settings forms instantiate cleanly with correct control dimensions, padding, and text boundaries without clipping.
