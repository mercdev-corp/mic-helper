## Why

Users who find Mic Helper useful may want to support the project or donate to the developer. Currently, neither the client nor server applications provide any link or action in their system tray context menus to facilitate donations, even though a repository `DONATE` file provides a donation URL (`https://ko-fi.com/mercdev_pro`). Adding a "Donate" context menu option below "Settings" in both the client and server tray applications gives users quick and discoverable access to the donation link.

## What Changes

- Add a `Donate` menu item to the system tray context menu in `ClientTrayApplicationContext`, positioned immediately below `Settings...`.
- Add a `Donate` menu item to the system tray context menu in `ServerTrayApplicationContext`, positioned immediately below `Settings...`.
- When the user clicks `Donate`, resolve the URL from the embedded `DONATE` resource and launch it in the system default web browser via OS shell execution.
- Embed the repository `DONATE` file as an embedded resource into `MicHelper.Shared` and provide a centralized helper (`DonateUrlProvider`) so both client and server applications reliably access the URL exclusively from the embedded resource without checking the filesystem.

## Capabilities

### New Capabilities
<!-- None -->

### Modified Capabilities
- `client-tray-app`: Update Context Menu Navigation to include a `Donate` option below `Settings...` that opens the URL specified in the embedded `DONATE` resource in the default browser.
- `server-tray-app`: Update Context Menu Navigation to include a `Donate` option below `Settings...` that opens the URL specified in the embedded `DONATE` resource in the default browser.

## Impact

- **UI Context Menus**: `ClientTrayApplicationContext` and `ServerTrayApplicationContext` tray context menus will now contain: Pause/Resume, Separator, Settings..., Donate, Separator, Exit.
- **Shared Infrastructure**: `MicHelper.Shared` will embed the `DONATE` resource and provide a safe browser URL launch utility with error handling.
- **Build System**: Ensure `DONATE` is embedded as an `EmbeddedResource` so single-file releases bundle the URL without requiring an external file alongside the executable.
- **Automated Tests**: Unit tests in `MicHelper.Tests` will verify that context menu items include `Donate` in the expected order, that URL loading extracts the valid URL from the embedded `DONATE` resource, and that the click handler triggers URL opening.
