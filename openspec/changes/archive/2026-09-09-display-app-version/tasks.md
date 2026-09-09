## 1. Build & Version Infrastructure

- [x] 1.1 Create `Directory.Build.props` at repository root to extract version from `VERSION` file, set MSBuild version properties (`Version`, `AssemblyVersion`, `FileVersion`, `InformationalVersion`), and set `IncludeSourceRevisionInInformationalVersion` to `false`. Verify `dotnet build` succeeds and sets assembly attributes.
- [x] 1.2 Implement `AppVersion` helper in `MicHelper.Shared.Common` providing `DisplayVersion` (e.g., `v0.1.0`) and `RawVersion`, with defensive fallback handling. Verify with unit tests in `MicHelper.Tests`.

## 2. Client Settings Window

- [x] 2.1 Update `ClientSettingsForm` to add a centered version label between `_btnOpenLogs` ("View Logs...") and `_btnClose` ("Close") displaying `AppVersion.DisplayVersion`. Verify client project compiles cleanly and control is placed correctly.

## 3. Server Settings Window

- [x] 3.1 Update `ServerSettingsForm` to add a centered version label between `_btnOpenLogs` ("View Logs...") and `_btnClose` ("Close") displaying `AppVersion.DisplayVersion`. Verify server project compiles cleanly and control is placed correctly.

## 4. Verification and Pipeline Integration

- [x] 4.1 Run full test suite via `dotnet test` and verify that all tests pass.
- [x] 4.2 Verify build pipeline compatibility by running `build_all.ps1` and checking that generated executables carry the version string from `VERSION`.
