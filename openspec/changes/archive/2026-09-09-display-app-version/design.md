## Context

Mic Helper consists of `MicHelper.Client`, `MicHelper.Server`, `MicHelper.Shared`, and `MicHelper.Tests`. The project version is maintained in a single plain text `VERSION` file at the root of the repository (e.g., containing `0.1.0`). GitHub Actions workflows automatically bump this version upon merging pull requests to `main` and create GitHub releases based on `VERSION`.

Currently, assemblies do not read from `VERSION` during standard compilation, and neither `ClientSettingsForm` nor `ServerSettingsForm` displays the running application version.

See `proposal.md` for background and motivation.

## Goals / Non-Goals

**Goals:**
- Inject the version from the repository's `VERSION` file into all project assemblies automatically at compile time via MSBuild.
- Provide a resilient, centralized version retriever (`AppVersion`) in `MicHelper.Shared` that formats the version string as `v{version}` (e.g., `v0.1.0`).
- Display the formatted version string centered between the `View Logs...` and `Close` buttons at the bottom of both `ClientSettingsForm` and `ServerSettingsForm`.
- Support automated builds via `build_all.ps1`, GitHub Actions release workflows, and standard `dotnet build` / `dotnet publish` commands.

**Non-Goals:**
- Implementing runtime check-for-updates or remote version queries.
- Modifying how GitHub Actions calculates or bumps the `VERSION` file.

## Decisions

### Decision 1: Root `Directory.Build.props` for compile-time injection
Inject version attributes across all projects in the solution by creating a root `Directory.Build.props` file:
```xml
<Project>
  <PropertyGroup>
    <VersionFilePath>$(MSBuildThisFileDirectory)VERSION</VersionFilePath>
    <AppRawVersion Condition="Exists('$(VersionFilePath)')">$([System.IO.File]::ReadAllText('$(VersionFilePath)').Trim())</AppRawVersion>
    <AppRawVersion Condition="'$(AppRawVersion)' == ''">0.1.0</AppRawVersion>
    
    <Version>$(AppRawVersion)</Version>
    <AssemblyVersion>$(AppRawVersion)</AssemblyVersion>
    <FileVersion>$(AppRawVersion)</FileVersion>
    <InformationalVersion>$(AppRawVersion)</InformationalVersion>
    <IncludeSourceRevisionInInformationalVersion>false</IncludeSourceRevisionInInformationalVersion>
  </PropertyGroup>
</Project>
```
*Rationale*:
`Directory.Build.props` is automatically evaluated by MSBuild for every project in the tree without altering individual project files. It works for `dotnet build`, `dotnet publish`, `dotnet test`, Visual Studio, and CI/CD pipelines without requiring manual `-p:Version=...` flags. A fallback condition ensures builds succeed even if `VERSION` is absent in unexpected build environments.

*Alternatives considered*:
- Passing `-p:Version` parameter only in `build_all.ps1`: Would not work during local IDE runs or standard `dotnet test` invocations.
- Source generator or code generation script: Overkill when MSBuild property functions natively read files.

### Decision 2: Centralized `AppVersion` helper in `MicHelper.Shared`
Create a static helper `MicHelper.Shared.Common.AppVersion` that:
- Reads the entry assembly's `AssemblyInformationalVersionAttribute` or falls back to `Assembly.GetName().Version`.
- Strips any trailing build metadata (such as commit hashes after `+`).
- Formats the version with a leading `v` (e.g., `v0.1.0`).
- Exposes `AppVersion.DisplayVersion` (e.g. `"v0.1.0"`) and `AppVersion.RawVersion` (e.g. `"0.1.0"`).

*Rationale*:
Ensures client and server display the exact same formatted string and guarantees that single-file self-contained executables (which do not ship with the raw `VERSION` file) reliably obtain their compile-time version at runtime.

### Decision 3: Centered version label in Settings forms
Both settings dialogs feature two bottom buttons:
- Left: `_btnOpenLogs` (`Location = (20, Y)`, `Width = 100`)
- Right: `_btnClose` (`Location = (260, Y)`, `Width = 100` or `(280, Y)`, `Width = 80`)

Add `_lblVersion` between them:
- Centered between the right edge of `_btnOpenLogs` and left edge of `_btnClose`.
- `TextAlign = ContentAlignment.MiddleCenter`.
- `ForeColor = SystemColors.GrayText` for a clean, non-intrusive appearance.
- AutoEllipsis enabled to handle any unexpected length.

## Risks / Trade-offs

- **[Risk]**: Single-file native publish trimming or stripping assembly metadata.
  - *Mitigation*: `AssemblyInformationalVersionAttribute` and `AssemblyFileVersionAttribute` are standard Win32 PE metadata embedded into the executable binary itself and preserved by single-file publishing.
- **[Risk]**: Trailing commit hash in informational version (e.g., `0.1.0+commit`).
  - *Mitigation*: `<IncludeSourceRevisionInInformationalVersion>false</IncludeSourceRevisionInInformationalVersion>` is set in `Directory.Build.props`, and `AppVersion` defensively trims at `+` delimiter.
