## Context

`OverlayForm` is implemented as an external desktop layered window (`WS_EX_TOPMOST`, `WS_EX_LAYERED`, `WS_EX_NOACTIVATE`, `WS_EX_TOOLWINDOW`, and `WS_EX_TRANSPARENT`). Under Windows Desktop Window Manager (DWM), all topmost windows share the topmost Z-order band. However, whenever an application or borderless game (such as Hunt: Showdown 1896) gains focus or reasserts topmost status, Windows places that active window at the top of the band. Because `OverlayForm` specifies `WS_EX_NOACTIVATE` and is transparent to mouse clicks, it never gains focus and is placed beneath newly focused borderless games unless its topmost Z-order is actively reasserted.

See proposal.md for motivation and problem background.

## Goals / Non-Goals

**Goals:**
- Proactively keep the overlay visibly rendered above borderless windowed games (including Hunt: Showdown 1896) and active desktop applications.
- Immediately reassert `HWND_TOPMOST` whenever an external window gains the foreground, without stealing focus or interrupting user input.
- Detect any Z-order occlusion where another window is positioned above `OverlayForm` and recover the top position during the active render cycle.
- Maintain zero CPU overhead while the overlay is hidden or idle.
- Maintain complete anti-cheat safety (100% out-of-process, zero DLL injection or memory inspection).

**Non-Goals:**
- Hooking DirectX/Vulkan/OpenGL graphics swapchains or injecting foreign processes (anti-cheat non-compliant).
- Overcoming legacy exclusive fullscreen (FSE) modes that bypass DWM desktop composition. Modern games running borderless windowed mode composite through DWM.

## Decisions

### Decision 1: Hybrid Event-Driven + Periodic Occlusion Checking

**Choice**:
1. **Foreground Event Hook (`EVENT_SYSTEM_FOREGROUND`)**: Register an out-of-process Windows Event Hook (`SetWinEventHook`) with flags `WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS`. When the user switches to or clicks into a game like Hunt: Showdown 1896, Windows emits `EVENT_SYSTEM_FOREGROUND`. If the overlay is currently visible, it immediately reasserts `HWND_TOPMOST` using `SetWindowPos(Handle, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE)`.
2. **Fast Occlusion Check during Pulse Ticks (`GW_HWNDPREV`)**: Some games continuously reassert topmost status on every frame. During `OnPulseTimerTick` (~60 FPS while active), evaluate `GetWindow(Handle, GW_HWNDPREV)`. If the return value is not `IntPtr.Zero`, another window is currently positioned above `OverlayForm` in Z-order; immediately call `SetWindowPos` to reclaim the top.
3. **Visibility State Re-assertion**: Call `SetWindowPos` whenever `StartPulsing()` is invoked and whenever WYSIWYG mode or live state transitions from hidden to visible.

**Alternatives Considered**:
- *Dedicated High-Frequency Polling Timer*: Running a 10ms-50ms timer regardless of state would waste CPU resources and contradict the zero-CPU idle requirement.
- *Unconditional `SetWindowPos` on every tick*: Calling `SetWindowPos` on every 16ms tick even when already at the top causes unnecessary Win32 window message chatter. Checking `GW_HWNDPREV != IntPtr.Zero` is an ultra-fast check that makes `SetWindowPos` conditional.

### Decision 2: Win32 API Interop Definitions

**Choice**:
In `src/MicHelper.Client/Overlay/Win32Native.cs`, expose:
- `SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags)`
- `GetWindow(IntPtr hWnd, uint uCmd)`
- `SetWinEventHook(uint eventMin, uint eventMax, IntPtr hmodWinEventProc, WinEventDelegate lpfnWinEventProc, uint idProcess, uint idThread, uint dwFlags)`
- `UnhookWinEvent(IntPtr hWinEventHook)`
- Constants: `HWND_TOPMOST = -1`, `SWP_NOSIZE = 0x0001`, `SWP_NOMOVE = 0x0002`, `SWP_NOACTIVATE = 0x0010`, `SWP_NOOWNERZORDER = 0x0200`, `GW_HWNDPREV = 3`, `EVENT_SYSTEM_FOREGROUND = 0x0003`, `WINEVENT_OUTOFCONTEXT = 0`, `WINEVENT_SKIPOWNPROCESS = 2`.

### Decision 3: Managed Delegate Lifecycle and Thread Safety

**Choice**:
- Store the `WinEventDelegate` as a private instance field in `OverlayForm` to prevent garbage collection while the hook is active.
- Register the hook upon handle creation and unhook cleanly in `Dispose` or form closure.
- Use `BeginInvoke` if the hook callback is triggered across threads or when synchronizing with UI state.

## Risks / Trade-offs

- **[Risk] Z-Order Fighting**: If another topmost overlay (e.g. an aggressive game launcher or another utility) also reasserts topmost Z-order simultaneously.
  → *Mitigation*: The `GW_HWNDPREV` check only fires when occluded, and `SWP_NOACTIVATE` ensures neither window steals focus or causes input hitching.
- **[Risk] Performance Impact when Hidden**:
  → *Mitigation*: The hook callback performs a fast `if (!Visible) return;` check and does nothing when the overlay is idle or hidden. The pulse timer is already stopped when hidden (zero-CPU idle).
