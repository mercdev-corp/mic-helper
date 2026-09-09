## Why

Gamers, streamers, and remote workers often use a dedicated audio setup or secondary streaming/work PC while playing full-screen or borderless games on their primary gaming rig. Existing microphone status tools either require polling (wasting CPU/GPU), cause anti-cheat detection by hooking into DirectX/game processes, or lack lightweight cross-machine notification. Mic Helper provides an ultra-lightweight, native, zero-polling network microphone status indicator with an anti-cheat-safe overlay and system tray application.

Users also need the ability to run multiple copies of the server and client simultaneously from different directories on the same PC (configured on distinct ports) to monitor and display statuses for different microphones independently with auto-startup support.

## What Changes

- Establish the core UDP multicast networking protocol with auto-discovery, heartbeat announcements, and state updates.
- Implement server-side Windows Core Audio integration for zero-polling hardware mute/unmute and device plug/unplug event tracking.
- Implement server system tray application with context menu, settings dialog, microphone selector with missing-device fallback, port in-use validation, and path-unique startup registration.
- Implement client topmost, click-through, anti-cheat-safe transparent overlay window with pulsing animation and cached pre-rendered scaling.
- Implement client system tray application with context menu, settings dialog, server IP auto-discovery dropdown with missing-server fallback, and WYSIWYG overlay drag/resize positioning.
- Implement multi-instance isolation enabling different copies of the server and client to run simultaneously from different folders on the same machine without config conflicts or startup key collisions.

## Capabilities

### New Capabilities
- `core-network-protocol`: UDP multicast datagram specification, heartbeat timing, auto-discovery, burst transmission on state change, and multi-instance port assignment.
- `server-audio-monitor`: Windows Core Audio API integration (`IMMDeviceEnumerator`, `IAudioEndpointVolume`, `IMMNotificationClient`), target microphone selection, device connect/disconnect handling, and event-driven mute detection.
- `server-tray-app`: Server system tray lifecycle, status icons, context menu, local JSON configuration (`mic-helper-server-settings.json`), in-use port validation, per-folder instance isolation, and path-unique Windows Run startup registration.
- `client-overlay-engine`: Anti-cheat-safe topmost layered click-through overlay window (`WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_TOPMOST | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW`), sine-wave pulsing opacity animation, multi-monitor geometry, and pre-rendered asset caching.
- `client-tray-app`: Client system tray lifecycle, status icons, context menu, local JSON configuration (`mic-helper-client-settings.json`), server IP discovery dropdown with offline strikethrough fallback, WYSIWYG overlay drag-and-drop / resize controller, and path-unique Windows Run startup registration.

### Modified Capabilities
<!-- None: This is the initial specification set for the project. -->

## Impact

- Targets .NET 10 with Native AOT for single-file, self-contained, low-memory executables.
- Requires Windows Core Audio (WASAPI / MMDevice API) available on Windows 10/11.
- Writes instance configuration exclusively to local application folders (`mic-helper-server-settings.json` and `mic-helper-client-settings.json`).
- Registers startup keys in Windows Registry `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` with instance-unique names derived from application executable paths.
- Network traffic is constrained to local LAN UDP multicast/unicast on user-specified ports.
