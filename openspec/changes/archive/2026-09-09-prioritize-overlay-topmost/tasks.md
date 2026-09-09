## 1. Win32 Native Interop

- [x] 1.1 Add `SetWindowPos`, `GetWindow`, `SetWinEventHook`, `UnhookWinEvent`, `WinEventDelegate`, and associated constants (`HWND_TOPMOST`, `GW_HWNDPREV`, `EVENT_SYSTEM_FOREGROUND`, `SWP_*`, `WINEVENT_*`) to `src/MicHelper.Client/Overlay/Win32Native.cs` and verify the project compiles with `dotnet build src/MicHelper.Client`.

## 2. Topmost Z-Order Management & Foreground Hooks

- [x] 2.1 Implement `ReassertTopmost()` helper in `OverlayForm` that calls `SetWindowPos(Handle, Win32Native.HWND_TOPMOST, 0, 0, 0, 0, ...)` with `SWP_NOACTIVATE | SWP_NOMOVE | SWP_NOSIZE` and invoke it during `StartPulsing()`, verifying with unit tests and code inspection.
- [x] 2.2 Register an out-of-process WinEvent hook (`EVENT_SYSTEM_FOREGROUND`) with a persisted `WinEventDelegate` in `OverlayForm` during handle creation, invoke `ReassertTopmost()` when visible, and clean up in `Dispose`, verifying proper resource lifecycle management.
- [x] 2.3 Add fast Z-order predecessor check (`Win32Native.GetWindow(Handle, GW_HWNDPREV) != IntPtr.Zero`) inside `OnPulseTimerTick` in `OverlayForm` to conditionally reassert topmost position when occluded, verifying zero overhead when already at the top.

## 3. Testing and Verification

- [x] 3.1 Add unit and integration tests in `tests/MicHelper.Tests` covering overlay topmost style flags, handle initialization, and unhook disposal, verifying with `dotnet test`.
- [x] 3.2 Run full solution build and test execution (`dotnet test MicHelper.sln`) to ensure clean compilation and all tests pass without regressions.
