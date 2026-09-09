## 1. Solution Setup & Build Pipeline

- [x] 1.1 Scaffold .NET 10 solution and project structures for Server, Client, and Shared libraries with Native AOT single-file publishing configuration, and verify `dotnet build` succeeds
- [x] 1.2 Implement `dev_prepare.ps1` and `build_all.ps1` scripts for automated dependency installation and native compilation into `./bin`, and verify clean compilation

## 2. Core Shared Protocol & Multi-Instance Management

- [x] 2.1 Implement UDP multicast protocol datagram serializer, magic headers, sequence tracking, and status payloads, and verify packet integrity via unit tests
- [x] 2.2 Implement folder-scoped single instance mutex locking (`MicHelperServer_<PathHash>` and `MicHelperClient_<PathHash>`), verifying duplicate launch prevention in the same folder and concurrent launches across different folders
- [x] 2.3 Implement Windows startup registry manager (`HKCU\...\Run`) with path-hash-unique keys, verifying add, remove, and status queries for independent folder instances

## 3. Server Audio Subsystem & Monitoring

- [x] 3.1 Implement Windows Core Audio COM interop (`IMMDeviceEnumerator`, `IAudioEndpointVolume`, `IMMNotificationClient`) for device enumeration, and verify discovery of active microphone endpoints
- [x] 3.2 Implement `IAudioEndpointVolumeCallback` for event-driven mute notifications, verifying instantaneous callbacks without CPU polling
- [x] 3.3 Implement microphone unplug and re-plug detection with retry probe timer, verifying graceful transition to `Disconnected` status and automatic reconnection

## 4. Server Networking & Tray Application

- [x] 4.1 Implement UDP multicast broadcaster with 1.0-second heartbeats and 3-packet immediate bursts on state transition, verifying transmission on configured ports
- [x] 4.2 Implement server settings persistence (`mic-helper-server-settings.json`), port in-use collision detection, and UI validation
- [x] 4.3 Implement server system tray icon, status visuals (Paused, Disconnected, Muted, Unmuted), context menu, and microphone dropdown with red strikethrough for missing devices

## 5. Client Networking & Overlay Engine

- [x] 5.1 Implement UDP multicast listener, server auto-discovery aggregation, and retry timeout watchdog, verifying offline detection when heartbeats cease
- [x] 5.2 Implement anti-cheat-safe layered click-through window (`WS_EX_TOPMOST | WS_EX_TRANSPARENT | WS_EX_LAYERED | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW`), verifying click-through behavior
- [x] 5.3 Implement sine-wave pulsing opacity animation with zero-CPU suspension when hidden or paused, verifying render timer stops when unmuted
- [x] 5.4 Implement pre-rendered bitmap scaling and asset caching on settings save, verifying cached assets load without runtime scaling

## 6. Client Tray Application & WYSIWYG Editor

- [x] 6.1 Implement client settings persistence (`mic-helper-client-settings.json`), opacity slider, and pulse frequency controls
- [x] 6.2 Implement interactive WYSIWYG overlay drag-and-drop repositioning and aspect-ratio-locked resizing in settings mode, verifying center coordinates and dimensions persist
- [x] 6.3 Implement client system tray icon, status visuals, context menu, and server IP discovery dropdown with offline red strikethrough fallback

## 7. End-to-End Integration & Multi-Instance Verification

- [x] 7.1 Verify simultaneous execution of two server and client copies from separate directories on distinct ports monitoring different microphones
- [x] 7.2 Verify independent auto-startup registry entries persist and launch correctly for multiple folder installations
