# Compatibility and technical details

Designed for Windows 11 on Intel or AMD x64 PCs. The reference device was the 2024 USB-C Magic Trackpad (A3120). Bluetooth, USB, battery reporting, and scrolling were used on the reference laptop. The v2.3 driver includes USB and Bluetooth hardware IDs for Gen 3. An A1535 owner also [reported success on Windows 11](https://github.com/vitoplantamura/MagicTrackpad2ForWindows/issues/20#issuecomment-4097637156). Local testing used the A3120. The installer supports x64 only; Windows on ARM and 32-bit Windows are not supported.

The community MSI and apps are unsigned. Windows can show an unknown-publisher prompt. The build downloads the official [MagicTrackpad2ForWindows v2.3 release](https://github.com/vitoplantamura/MagicTrackpad2ForWindows/releases/tag/v2.3), checks the archive against its pinned SHA-256, and verifies the Microsoft catalog signature. The MSI includes those files and invokes Windows PnPUtil to install the Microsoft-signed driver. The community project has no affiliation with Apple or Microsoft.

Verify the MSI with PowerShell and compare the output to SHA256SUMS.txt on the same release:

~~~powershell
Get-FileHash .\Magic-Trackpad-Setup-1.7.9-x64.msi -Algorithm SHA256
~~~

The battery monitor runs elevated to use the driver's Bluetooth battery interface. Its readings and discharge history stay on the PC. Runtime estimates include idle time while connected. Pairing is manual.

The MSI installs the self-contained .NET 10 and WinUI runtime in Program Files. USB battery queries use a local named pipe with identification-only access and bounded waits. Startup uses one Windows scheduled task per PC. On a shared PC, enabling startup from another administrator account can change which account starts the monitor.

## Remove or repair

Use **Settings → Apps → Installed apps → Magic Trackpad Setup for Windows** to uninstall. Setup closes the app and battery indicator, removes their shortcuts and startup task, and clears battery history, update downloads, and app preferences for the uninstalling user. It removes the bundled driver only if this installer originally added it. A pre-existing driver, or one whose ownership cannot be established, is preserved. Windows manages its own installer cache and Bluetooth pairing.

Upgrades retain settings and battery history. Run the installed version’s MSI and choose **Repair** to restore missing or damaged files, or **Change** to add or remove the companion app.

## Installer choices and updates

One x64 MSI offers two installation choices. The recommended setup includes the driver, control panel, and taskbar battery indicator. Driver only installs the signed driver and its accompanying source and license. Windows Settings remains available for standard gestures. To add or remove the companion app later, run the same MSI and select **Change**. For unattended driver-only setup, set `ADDLOCAL=Main`. Full setup uses `ADDLOCAL=Main,App`.

In the full app, use **App settings → Check for updates**. A check contacts the official GitHub repository for its latest stable release. When a newer x64 MSI is available, the panel downloads it to Program Files and verifies its size and GitHub SHA-256 digest. **Update on next launch** saves the verified MSI in the installation’s protected update cache. Closing settings or shutting down preserves the download. The next settings launch or enabled sign-in launch claims it and checks its SHA-256 again before installation. A sign-in update starts the tray monitor in the background. **Restart and update** applies the downloaded MSI and reopens the panel. Windows Installer runs silently during this step. The update follows the same unsigned community release trust model as a manual download.

## Build from source

On Windows x64 with the .NET Framework 4.x compiler and the .NET 10 SDK, install WiX CLI 4.0.6 and its matching UI extension. From the repository root run:

~~~powershell
dotnet tool install --global wix --version 4.0.6
wix extension add WixToolset.UI.wixext/4.0.6 -g
.\Build.ps1
~~~

The standalone MSI and its `SHA256SUMS.txt` checksum file are written to `dist`. The src folder contains the C# setup app, tray monitor and icon renderer. The installer folder contains the WiX project. The MIT license covers the original code in this repository. The included v2.3 driver and WinUI control panel belong to the [upstream project](https://github.com/vitoplantamura/MagicTrackpad2ForWindows) and retain their GPLv2 license. The settings panel in panel/ is a GPLv2 adaptation with revised layout, motion, and English text. Corresponding sources and the license are installed under ThirdParty.

A direct MSI upgrade closes the installed control panel and battery monitor before Windows Installer checks for locked files. Windows may request administrator approval to close elevated processes. Save pending settings before continuing. Interactive setup offers **Launch Magic Trackpad**, checked by default, on its final page. Clearing it leaves the app closed. This does not change the separate **Open at startup** preference. Driver-only setup and uninstall do not offer app launch. Silent upgrades resume the enabled battery startup task.
