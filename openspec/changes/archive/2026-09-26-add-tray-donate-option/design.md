## Context

Both `MicHelper.Client` (`ClientTrayApplicationContext`) and `MicHelper.Server` (`ServerTrayApplicationContext`) construct system tray icons (`NotifyIcon`) with a context menu (`ContextMenuStrip`). Currently, both menus have the structure:
1. `Pause` / `Resume`
2. Separator
3. `Settings...`
4. Separator
5. `Exit`

The repository root contains a `DONATE` file containing a donation link (`https://ko-fi.com/mercdev_pro`). Users need a direct way from the tray icon to open this URL in their default browser.

See `proposal.md` for motivation and background.

## Goals / Non-Goals

**Goals:**
- Provide a `Donate` option positioned directly below `Settings...` in both Client and Server tray icon context menus.
- When clicked, reliably open the donation URL in the user's default web browser.
- Ensure the URL from `DONATE` is available in all deployment scenarios, including single-file self-contained `.exe` distributions (`PublishSingleFile=true`) and standard development runs.
- Gracefully handle edge cases (malformed URL, shell launch failure) without crashing the application.
- Add comprehensive automated unit tests for menu structure and URL resolution.

**Non-Goals:**
- In-app payment processing or custom webview embedding.
- Altering the appearance or behavior of existing menu options (`Pause`/`Resume`, `Settings...`, `Exit`).

## Decisions

### Decision 1: Centralized `DonateUrlProvider` in `MicHelper.Shared` with Embedded Resource

- **Approach**: Embed the repo root `DONATE` file as an `EmbeddedResource` in `MicHelper.Shared.csproj`. Implement a static helper class `DonateUrlProvider` in `MicHelper.Shared.Common` that:
  1. Reads the embedded `DONATE` resource from `MicHelper.Shared` without inspecting the filesystem.
  2. Trims whitespace and validates that the string is a valid absolute HTTP/HTTPS URL.
  3. Returns the validated URL string or a safe fallback.
- **Rationale**: Single-file published executables (`build_all.ps1` with `-p:PublishSingleFile=true`) do not carry external text files alongside the binary. Embedding `DONATE` into the assembly metadata ensures the URL is always available at runtime without requiring an external file or checking the disk, while maintaining source-of-truth in the repository's `DONATE` file.
- **Alternatives considered**:
  - *Read from filesystem*: Fails when users download standalone releases where `DONATE` is not present next to the executable, and risks unexpected behavior from external disk files.
  - *Hardcode URL in C# source*: Decouples the code from the `DONATE` file and creates duplication.

### Decision 2: System Browser Launch via `ProcessStartInfo`

- **Approach**: Implement a helper method `UrlLauncher.OpenUrl(string url)` in `MicHelper.Shared.Common` (or within `DonateUrlProvider`):
  ```csharp
  Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
  ```
  Wrap the invocation in a `try...catch` block. On failure, log the error with `AppLogger.Error` and display a user-friendly `MessageBox` warning rather than allowing an unhandled exception.
- **Rationale**: In .NET Core / .NET 8 on Windows, `UseShellExecute = true` correctly invokes the OS default browser protocol handler.
- **Alternatives considered**:
  - *Invoking `cmd /c start <url>`*: Incurs console window flashing and shell escaping issues.

### Decision 3: Context Menu Layout and Ordering

- **Approach**: In both `ClientTrayApplicationContext.cs` and `ServerTrayApplicationContext.cs`, initialize `_menuDonate = new ToolStripMenuItem("Donate", null, OnDonateClicked);` and assemble the context menu items as:
  1. `_menuPauseResume`
  2. `ToolStripSeparator`
  3. `_menuSettings`
  4. `_menuDonate`
  5. `ToolStripSeparator`
  6. `_menuExit`
- **Rationale**: Places `Donate` immediately below `Settings...`, grouped cleanly between the settings action and the exit lifecycle command.

### Decision 4: Testability and Verification

- **Approach**:
  - Expose internal accessors for `ContextMenuStrip` or `_menuDonate` on `ClientTrayApplicationContext` and `ServerTrayApplicationContext`.
  - Add unit tests verifying:
    - `DonateUrlProvider` correctly parses and returns the expected URL from the `DONATE` resource.
    - `ClientTrayApplicationContext` context menu contains `Donate` at the expected index (immediately after `Settings...`).
    - `ServerTrayApplicationContext` context menu contains `Donate` at the expected index (immediately after `Settings...`).
- **Rationale**: Ensures regression prevention and validates requirement adherence without requiring physical UI clicks.

## Risks / Trade-offs

- **[Risk] Single-file binary missing `DONATE` file** → **Mitigation**: Embed `DONATE` via `<EmbeddedResource Include="..\..\DONATE" Link="DONATE" />` in `MicHelper.Shared.csproj`.
- **[Risk] Browser launch failure on restricted environments** → **Mitigation**: Catch exceptions from `Process.Start`, log details via `AppLogger`, and show a friendly error dialog.
- **[Risk] Malformed URL in `DONATE` file** → **Mitigation**: Validate URL format with `Uri.TryCreate` ensuring `Uri.UriSchemeHttp` or `Uri.UriSchemeHttps` before attempting shell execution.
