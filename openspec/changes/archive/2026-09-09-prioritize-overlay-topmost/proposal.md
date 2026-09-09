## Why

Certain games and applications running in borderless windowed mode (such as Hunt: Showdown 1896) set themselves as the active topmost window or reassert topmost Z-order upon gaining focus. Because the microphone helper overlay is an inactive, non-focusable window (`WS_EX_NOACTIVATE`), it gets layered underneath these games, causing status indicators (e.g. muted or disconnected microphone) to become invisible to the user during gameplay.

## What Changes

- Implement dynamic topmost Z-order re-assertion for the client overlay window using Win32 API calls (`SetWindowPos` with `HWND_TOPMOST` and `SWP_NOACTIVATE`).
- Introduce an out-of-process WinEvent hook (`EVENT_SYSTEM_FOREGROUND`) to immediately reassert overlay topmost placement whenever any external window (such as a game) is brought to the foreground.
- Add an overlay topmost watchdog check during active animation timer ticks (`OnPulseTimerTick`) using fast Z-order predecessor checking (`GW_HWNDPREV`), repositioning the overlay to the top if any other window occludes it in Z-order.
- Ensure the overlay reasserts topmost Z-order upon initial display and whenever transitioning from hidden to visible.
- Preserve anti-cheat safety: maintain non-invasive window management without process injection, memory reading, or foreign game API hooking.

## Capabilities

### New Capabilities
<!-- None -->

### Modified Capabilities
- `client-overlay-engine`: Update requirements to enforce proactive topmost Z-order priority maintenance over borderless windowed and fullscreen applications, foreground window change detection, and periodic topmost occlusion recovery without stealing focus or violating anti-cheat integrity.

## Impact

- **Affected Code**:
  - `src/MicHelper.Client/Overlay/Win32Native.cs`: Add P/Invoke definitions for `SetWindowPos`, `GetWindow`, `SetWinEventHook`, `UnhookWinEvent`, and associated flags/constants.
  - `src/MicHelper.Client/Overlay/OverlayForm.cs`: Implement foreground event hook listener, topmost enforcement helper, and periodic Z-order checks during render pulses.
- **Dependencies & Performance**:
  - Pure Win32 API additions; no third-party libraries required.
  - Zero performance impact when the overlay is idle or hidden (hook and timer do zero work or remain dormant).
