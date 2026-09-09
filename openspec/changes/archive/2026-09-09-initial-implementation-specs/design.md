## Context

Mic Helper requires extreme low-resource consumption (CPU, RAM, GPU, network) to avoid degrading gaming performance while running alongside modern titles. Additionally, overlay graphics must never trigger anti-cheat software (e.g. Easy Anti-Cheat, BattlEye, Vanguard). See `proposal.md` for overall motivation.

This design details the technical architecture for .NET 10 Native AOT compilation, Windows Core Audio integration, UDP multicast networking, click-through overlay window management, and multi-instance folder isolation.

## Goals / Non-Goals

**Goals:**
- Deliver native single-file executables via .NET 10 Native AOT with sub-25MB memory usage and 0% idle CPU overhead.
- Guarantee anti-cheat safety by avoiding DLL injection, memory inspection, and DirectX/graphics API hooks.
- Provide instantaneous mute status propagation (<50ms) using event-driven COM callbacks without polling.
- Allow running multiple isolated server and client instances simultaneously from different directories on different ports, each with independent settings and auto-startup entries.
- Support full WYSIWYG overlay positioning and aspect-ratio-locked resizing with pre-rendered cached bitmaps.

**Non-Goals:**
- Audio streaming or audio signal processing (only mute/device status is handled).
- Internet-wide cloud signaling or relay servers (LAN UDP multicast is used).
- Fullscreen exclusive hardware video mode takeover (standard Windows desktop layered window over borderless/windowed games).

## Decisions

### 1. Framework & UI: Windows Forms with Native AOT and Win32 P/Invoke
- **Rationale**: Windows Forms compiled under .NET 10 Native AOT produces small native binaries (~15MB), boots instantly, natively supports Windows `NotifyIcon` (system tray), and allows low-level Win32 window style manipulation via `CreateParams` / `SetWindowLongPtr`.
- **Alternatives considered**:
  - *WPF*: Heavy runtime footprint, historically problematic Native AOT compatibility, excessive memory overhead.
  - *Pure Win32 / C++*: Excellent performance, but lacks rapid developer productivity and .NET 10 toolchain parity.

### 2. Audio Capture: Windows Core Audio COM Interfaces
- **Rationale**: `IMMDeviceEnumerator`, `IMMNotificationClient`, and `IAudioEndpointVolumeCallback` deliver native OS-level event notifications directly into C# callbacks when mute status changes or audio devices are connected/disconnected. Zero idle CPU polling is required.
- **Alternatives considered**:
  - *Periodic polling via winmm / waveIn*: Constantly consumes CPU ticks and introduces status notification lag.

### 3. Network Architecture: UDP Multicast with Event Bursts
- **Rationale**: UDP multicast (default group `239.255.132.5`) allows servers to announce presence and broadcast status updates without maintaining open TCP socket connections or tracking client IP addresses. To counteract potential UDP drop over Wi-Fi, the server transmits a burst of 3 duplicate datagrams within 50ms upon state transition, supplemented by a 1.0-second background heartbeat.
- **Alternatives considered**:
  - *TCP WebSockets / HTTP*: High connection maintenance overhead, complex reconnect state machines, and unnecessary server load.
  - *Named Pipes*: Local machine only; cannot span LAN across separate PCs.

### 4. Overlay Architecture: Topmost Layered Click-Through Window
- **Rationale**: A Win32 window created with:
  ```c
  WS_EX_TOPMOST | WS_EX_TRANSPARENT | WS_EX_LAYERED | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW
  ```
  ensures the window remains atop borderless games, passes all mouse clicks directly through to the underlying application, never steals focus, and does not appear in Alt+Tab. Because this relies solely on documented Windows desktop windowing, anti-cheat systems do not classify it as malicious.
- **Alternatives considered**:
  - *DirectX / D3D11 hook (e.g. Present hooking)*: Directly triggers anti-cheat bans and causes game crashes.

### 5. Performance Optimization: Pre-Rendered Asset Resizing
- **Rationale**: Runtime image scaling incurs continuous CPU/GPU resampling during pulsing animation. When the user modifies overlay size in settings, the resulting bitmap is pre-rendered at the exact final dimensions and cached to the local folder upon closing settings. Runtime pulse animation only updates per-frame window alpha (`SetLayeredWindowAttributes` or `UpdateLayeredWindow`).

### 6. Multi-Instance Isolation & Auto-Startup Architecture
- **Rationale**:
  - **Isolated Settings**: Settings are loaded from/written to the application's working directory (`mic-helper-server-settings.json` and `mic-helper-client-settings.json`).
  - **Folder-Scoped Mutex**: Single-instance mutexes are keyed by `MicHelperServer_<PathHash>` and `MicHelperClient_<PathHash>`. Launching the same executable twice is prevented, but launching from different directories is explicitly permitted.
  - **Unique Startup Registry Keys**: To support running multiple copies from different folders on Windows startup, registry values in `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` use distinct names formatted as `MicHelperServer_<PathHash>` and `MicHelperClient_<PathHash>` pointing to the exact executable path.

## Risks / Trade-offs

- **[UDP Multicast blocked by complex corporate networks or guest Wi-Fi]** → Default to LAN multicast; if multicast is filtered by a router, users can configure directed broadcast or client-specified server IP.
- **[Exclusive fullscreen game overriding desktop topmost windows]** → True exclusive fullscreen captures the display hardware directly; documentation recommends borderless windowed mode, which is standard for modern games and overlay utilities.
- **[Unplugged device or offline server handling]** → The system preserves the user's configured device or server IP in settings even when offline, marking it with red strikethrough styling in the UI and probing periodically rather than clearing user preferences.
