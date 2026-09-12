## Why

When multiple audio capture devices are connected, Windows distinguishes between the "Default device" (Console/Multimedia, set via the primary radio button in Windows Sound Settings) and the "Default communications device". The audio monitor currently queries `ERole.eCommunications` before `ERole.eConsole`, which can bind to an auxiliary virtual communications device (such as "Wave Cast") instead of the user's primary selected microphone (such as "Microphone"), causing mute detection mismatches and confusing `(Default)` tags in the dropdown. Additionally, settings dialogs have informational text labels and hints that extend to the right edge of the window without proper padding or wrapping.

## What Changes

- **Default Audio Endpoint Role Prioritization**: In `WindowsAudioMonitor.cs`, update `GetDefaultCaptureDevice()` to prioritize `ERole.eConsole` (the standard Windows default device) before falling back to `ERole.eCommunications` and `ERole.eMultimedia`, ensuring default device detection matches the device selected in Windows Sound Settings.
- **Clarified Default Role Indications in UI**: Update `MicrophoneSelectionController` (used by both Server and Client settings forms) to accurately label default devices. If a device is the default console/general device, label it `(Default)`; if another device is the default communications device, label it `(Default Communications)`.
- **Settings Dialog Padding and Layout Fixes**: 
  - In `ClientSettingsForm.cs`, ensure labels such as `_lblDualPcNote` ("Run server app on remote PC where your Microphone is plugged in") and `lblHint` ("Drag overlay to move • Scroll mouse wheel or drag corners to resize.") have proper margins/padding, wrapping or constraints so text does not clip or touch the right window border.
  - Expand client area width or adjust control bounds in both `ClientSettingsForm.cs` and `ServerSettingsForm.cs` so all text elements maintain consistent 20px padding from the right edge across varying system DPIs.

## Capabilities

### New Capabilities
<!-- None -->

### Modified Capabilities
- `server-audio-monitor`: Update default capture device fallback requirement to prioritize `ERole.eConsole` before `ERole.eCommunications`.
- `client-tray-app`: Add requirement for settings dialog text margin and wrapping guarantees, and distinct default device role labeling in Single PC microphone selection.
- `server-tray-app`: Add requirement for settings dialog text margin guarantees and distinct default device role labeling in server microphone selection.

## Impact

- **Code Projects**:
  - `src/MicHelper.Shared`: `WindowsAudioMonitor.cs` (default capture resolution), `AudioDeviceInfo.cs` (default role flags/tracking), `MicrophoneSelectionController.cs` (dropdown item formatting).
  - `src/MicHelper.Client`: `ClientSettingsForm.cs` (window size, layout, text wrapping, and right padding).
  - `src/MicHelper.Server`: `ServerSettingsForm.cs` (window size, layout, and right padding).
- **Behavioral Changes**: Users who rely on default microphone resolution will track the microphone selected as the Default Device in Windows Sound Settings rather than an unintended communications device.
- **Backward Compatibility**: Fully backward compatible. Does not alter saved configuration formats or network protocol.
