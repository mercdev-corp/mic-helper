## Why

With automated release pipelines bumping the version in `VERSION` during PR merge to `main`, users and maintainers need a quick, clear way to verify which version of the Mic Helper Client and Server applications is currently running. Currently, neither settings dialog displays the application version, and the version is not systematically embedded into compiled application binaries from the `VERSION` file during build time.

Embedding the version at compile time and displaying it prominently in both settings dialogs ensures consistency with GitHub release tags and provides immediate operational visibility.

## What Changes

- Embed the version from the root `VERSION` file into all compiled assemblies during build and publish via MSBuild configuration (`Directory.Build.props`).
- Expose a centralized version retrieval helper in `MicHelper.Shared` (`AppVersion`) to provide formatted version strings (e.g., `v0.1.0`).
- Update `ClientSettingsForm` to display the application version label at the bottom of the settings window, positioned between the `View Logs...` and `Close` buttons.
- Update `ServerSettingsForm` to display the application version label at the bottom of the settings window, positioned between the `View Logs...` and `Close` buttons.
- Ensure automated GitHub release workflows (`release.yml`) and build scripts (`build_all.ps1`) produce binaries containing the exact version string matching the release tag.

## Capabilities

### New Capabilities
<!-- None -->

### Modified Capabilities
- `client-tray-app`: Add requirement to display the embedded application version at the bottom of the Client Settings dialog between the "View Logs..." and "Close" buttons.
- `server-tray-app`: Add requirement to display the embedded application version at the bottom of the Server Settings dialog between the "View Logs..." and "Close" buttons.

## Impact

- **Build System**: Introduces `Directory.Build.props` at repository root to automatically inject `Version`, `AssemblyVersion`, `FileVersion`, and `InformationalVersion` from `VERSION` during all `dotnet build` and `dotnet publish` invocations.
- **Shared Code**: Adds a centralized version provider in `MicHelper.Shared.Common`.
- **UI**: Adjusts layout in `ClientSettingsForm` and `ServerSettingsForm` to render a centered, muted/read-only version label between the bottom action buttons.
- **Release Automation**: Executables compiled in the release workflow will carry the exact version specified in `VERSION` in assembly metadata and UI.
