## MODIFIED Requirements

### Requirement: Anti-Cheat Safe Click-Through Overlay Window
The client overlay SHALL be implemented as an external desktop layered window with `WS_EX_TOPMOST`, `WS_EX_TRANSPARENT`, `WS_EX_LAYERED`, `WS_EX_NOACTIVATE`, and `WS_EX_TOOLWINDOW` styles, without hooking game APIs or inspecting foreign processes, and SHALL remain visibly positioned on top of borderless windowed and fullscreen games.

#### Scenario: User clicks game screen beneath overlay
- **WHEN** the overlay is visible on top of an active borderless or fullscreen game window and the user clicks over the overlay region
- **THEN** all mouse and keyboard inputs pass directly through to the underlying application or game without interception or latency

#### Scenario: Overlay displays over fullscreen applications
- **WHEN** a game or application runs in fullscreen or borderless windowed mode
- **THEN** the overlay remains rendered on top of the display without stealing focus or activating a window title bar

## ADDED Requirements

### Requirement: Proactive Topmost Z-Order Enforcement
The client overlay engine SHALL proactively maintain topmost Z-order over all desktop applications and games (including borderless windowed games that assert topmost Z-order) by reasserting its topmost position upon visibility changes, external window foreground transitions, and active animation ticks without stealing input focus.

#### Scenario: Borderless game activates or gains focus
- **WHEN** an external application or borderless windowed game (such as Hunt: Showdown 1896) gains focus or activates
- **THEN** the overlay receives the foreground change event and reasserts `HWND_TOPMOST` without stealing input focus

#### Scenario: Overlay occluded by another topmost window during active alert
- **WHEN** the overlay is visible in an alert state and another window is positioned above it in the Z-order
- **THEN** the overlay detects the occlusion during its active update cycle and immediately repositions itself to the top of the Z-order

#### Scenario: Overlay transitions from hidden to visible
- **WHEN** the overlay transitions from hidden to visible upon entering an alert state
- **THEN** the overlay immediately reasserts `HWND_TOPMOST` to ensure it is displayed above currently running borderless games
