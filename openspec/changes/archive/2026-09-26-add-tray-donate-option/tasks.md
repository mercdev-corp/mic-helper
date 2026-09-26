## 1. Shared Donation Infrastructure

- [x] 1.1 Embed the repository `DONATE` file as an `EmbeddedResource` in `src/MicHelper.Shared/MicHelper.Shared.csproj`. Verify the project compiles and the resource is embedded.
- [x] 1.2 Implement `DonateUrlProvider` in `src/MicHelper.Shared/Common/DonateUrlProvider.cs` to resolve the URL exclusively from the embedded `DONATE` resource, validate URL format, and provide a safe browser launch helper that catches errors and logs via `AppLogger`.
- [x] 1.3 Add unit tests in `tests/MicHelper.Tests/DonateUrlProviderTests.cs` to verify URL resolution, whitespace trimming, and invalid URL handling.

## 2. Client Tray Menu

- [x] 2.1 In `src/MicHelper.Client/ClientTrayApplicationContext.cs`, instantiate `_menuDonate = new ToolStripMenuItem("Donate", null, OnDonateClicked)` and insert it immediately below `_menuSettings` in `_contextMenu.Items`. Implement `OnDonateClicked` to invoke `DonateUrlProvider.OpenDonationPage()`.
- [x] 2.2 Add unit tests in `tests/MicHelper.Tests/ClientOverlayAndSettingsTests.cs` (or dedicated test) to verify that `ClientTrayApplicationContext`'s context menu contains `Donate` directly below `Settings...` and ahead of `Exit`.

## 3. Server Tray Menu

- [x] 3.1 In `src/MicHelper.Server/ServerTrayApplicationContext.cs`, instantiate `_menuDonate = new ToolStripMenuItem("Donate", null, OnDonateClicked)` and insert it immediately below `_menuSettings` in `_contextMenu.Items`. Implement `OnDonateClicked` to invoke `DonateUrlProvider.OpenDonationPage()`.
- [x] 3.2 Add unit tests in `tests/MicHelper.Tests/ServerNetworkingAndSettingsTests.cs` (or dedicated test) to verify that `ServerTrayApplicationContext`'s context menu contains `Donate` directly below `Settings...` and ahead of `Exit`.

## 4. Verification & Solution Build

- [x] 4.1 Run `dotnet test tests/MicHelper.Tests/MicHelper.Tests.csproj` and verify all tests pass without errors.
- [x] 4.2 Verify client and server compilation via `dotnet build MicHelper.sln` to confirm clean builds and resource bundling across both apps.
