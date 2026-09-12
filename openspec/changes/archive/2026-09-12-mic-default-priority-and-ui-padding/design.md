## Context

See `proposal.md` for motivation and background.

Currently in `WindowsAudioMonitor.cs`:
```csharp
int hr = _deviceEnumerator.GetDefaultAudioEndpoint(EDataFlow.eCapture, ERole.eCommunications, out var device);
if (hr != 0 || device == null)
{
    hr = _deviceEnumerator.GetDefaultAudioEndpoint(EDataFlow.eCapture, ERole.eConsole, out device);
}
```
Windows Core Audio defines three endpoint roles:
- `ERole.eConsole`: The primary Windows default device for system sounds and general recording (the device selected with the radio button in Windows 11 Sound Settings).
- `ERole.eCommunications`: The default voice communications device (used by telephony and voice chat applications).
- `ERole.eMultimedia`: Used for music and media recording.

Because `WindowsAudioMonitor` queries `eCommunications` before `eConsole`, machines with distinct communications capture endpoints (such as Elgato Wave Cast, virtual audio cables, or chat headsets) select the communications device as the default rather than the device selected in Windows Settings.

In `ClientSettingsForm.cs` and `ServerSettingsForm.cs`:
- Client area width is fixed at 380px (`ClientSize = new Size(380, ...)`).
- Left margin is 20px, controls are 340px wide, leaving 20px right margin.
- However, labels with `AutoSize = true` and single-line text (specifically `_lblDualPcNote`: "Run server app on remote PC where your Microphone is plugged in" and `lblHint`: "Drag overlay to move • Scroll mouse wheel or drag corners to resize.") exceed 350px in length depending on system font metrics, causing text to run directly into or clip at the right window boundary.

## Goals / Non-Goals

**Goals:**
- Align default capture device resolution in `WindowsAudioMonitor` with the Windows Settings default device (`ERole.eConsole` first).
- Clearly differentiate between `(Default)` and `(Default Communications)` devices in the settings microphone dropdown when the system configures different devices for each role.
- Provide a minimum of 20px right padding across all controls and labels in both `ClientSettingsForm` and `ServerSettingsForm`, wrapping long notes cleanly without truncation.

**Non-Goals:**
- Intercepting proprietary USB HID hardware mute buttons (e.g. Elgato Wave capacitive tap sensor) or communicating with local WebSocket services. This design focuses on OS-level Core Audio endpoint volume and Windows settings routing.
- Changing saved settings JSON schema or network packet protocol.

## Decisions

### Decision 1: Reorder Default Capture Device Resolution in `WindowsAudioMonitor.cs`
- **Choice**: Query `ERole.eConsole` first. If unavailable, query `ERole.eCommunications`, then `ERole.eMultimedia`.
  ```csharp
  int hr = _deviceEnumerator.GetDefaultAudioEndpoint(EDataFlow.eCapture, ERole.eConsole, out var device);
  if (hr != 0 || device == null)
  {
      hr = _deviceEnumerator.GetDefaultAudioEndpoint(EDataFlow.eCapture, ERole.eCommunications, out device);
  }
  if (hr != 0 || device == null)
  {
      hr = _deviceEnumerator.GetDefaultAudioEndpoint(EDataFlow.eCapture, ERole.eMultimedia, out device);
  }
  ```
- **Rationale**: The Windows 11 Sound Settings UI sets `eConsole` when the user selects a default input device via the radio button. Prioritizing `eConsole` ensures MicHelper targets the device the user expects.
- **Alternatives Considered**: Keeping `eCommunications` first. Rejected because it causes persistent mismatch with Windows 11 Sound Settings.

### Decision 2: Distinct Role Identification in `AudioDeviceInfo` and `MicrophoneSelectionController`
- **Choice**:
  - Update `GetActiveCaptureDevices()` to resolve both `eConsole` and `eCommunications` endpoint IDs.
  - Enhance `AudioDeviceInfo` (or provide helper properties) with `bool IsDefaultConsole` and `bool IsDefaultCommunications`.
  - In `MicrophoneSelectionController.Populate`, label items:
    - If `IsDefaultConsole && IsDefaultCommunications` (or only console is default): `"{Name} (Default)"`
    - If distinct: console default is `"{Name} (Default)"` and communications default is `"{Name} (Default Communications)"`
- **Rationale**: Gives full transparency in the dropdown without confusing users as to which device is the system default.

### Decision 3: Settings Dialog Width Expansion and Text Wrapping
- **Choice**:
  - Increase `ClientSize.Width` to `400` in `ClientSettingsForm` and `ServerSettingsForm` (giving 360px content width with 20px left/right margins).
  - Update `_lblDualPcNote` and `lblHint`: set `MaximumSize = new Size(360, 0)`, `AutoSize = true` to allow automatic word-wrapping if text exceeds 360px on high-DPI displays.
  - Adjust input controls (comboboxes, trackbars) to width `360` with location `X = 20`.
  - Ensure action buttons and version label at the bottom are evenly positioned within the 400px width.
- **Rationale**: Fixes text crowding at the right edge permanently across all font scaling factors and DPI settings.

## Risks / Trade-offs

- **[Risk]** Existing unit tests expecting specific default device mock behaviors.
  - **Mitigation**: Update test assertions in `AudioMonitorTests` and `ServerNetworkingAndSettingsTests` to reflect `eConsole` priority and new default label formatting.
- **[Risk]** Form height increasing slightly if multi-line text wraps on very small resolutions.
  - **Mitigation**: Constrain labels with `MaximumSize` and allocate vertical spacing so wrapped text does not overlap adjacent controls.
