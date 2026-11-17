# Compatibility and technical details

Designed for Windows 11 on Intel or AMD x64 PCs. The reference device was the 2024 USB-C Magic Trackpad (A3120). Bluetooth, USB, battery reporting, and scrolling were used on the reference laptop. The v2.3 driver includes USB and Bluetooth hardware IDs for Gen 3. An A1535 owner also [reported success on Windows 11](https://github.com/vitoplantamura/MagicTrackpad2ForWindows/issues/20#issuecomment-4097637156). Local testing used the A3120. Windows on ARM has not been validated.

The community MSI and apps are unsigned. Windows can show an unknown-publisher prompt. The build downloads the official [MagicTrackpad2ForWindows v2.3 release](https://github.com/vitoplantamura/MagicTrackpad2ForWindows/releases/tag/v2.3), checks the archive against its pinned SHA-256, and verifies the Microsoft catalog signature. The MSI includes those files and invokes Windows PnPUtil to install the Microsoft-signed driver. The community project has no affiliation with Apple or Microsoft.

Verify the MSI with PowerShell and compare the output to SHA256SUMS.txt on the same release:

~~~powershell
Get-FileHash .\Magic-Trackpad-Setup-1.4.0-x64.msi -Algorithm SHA256
~~~

The battery monitor runs elevated to use the driver's Bluetooth battery interface. Its readings and discharge history stay on the PC. Runtime estimates include idle time while connected. Pairing is manual.

The MSI installs the self-contained .NET 10 and WinUI runtime in Program Files. USB battery queries use a local named pipe with identification-only access and bounded waits. Startup uses one Windows scheduled task per PC. On a shared PC, enabling startup from another administrator account can change which account starts the monitor.

## Remove or repair

Exit the battery monitor from its tray menu and close the control panel. Use **Settings > Apps > Installed apps > Magic Trackpad Setup for Windows** to uninstall. This removes the apps and their shortcuts and leaves the trackpad driver installed. To repair the installation, use Windows Installer repair.

## Installer choices and updates in 1.6

One x64 MSI offers two installation choices. The recommended setup includes the driver, control panel, and taskbar battery indicator. Driver only installs the signed driver and its accompanying source and license. Windows Settings remains available for standard gestures. To add or remove the companion app later, run the same MSI and select **Change**. For unattended driver-only setup, set `ADDLOCAL=Main`. Full setup uses `ADDLOCAL=Main,App`.

In the full app, use **App settings → Check for updates**. A check contacts the official GitHub repository for its latest stable release. When a newer x64 MSI is available, the panel downloads it to Program Files and verifies its size and GitHub SHA-256 digest. **Later** keeps the verified download available while the window is open. **Restart and update** runs Windows Installer and reopens the panel. The update follows the same unsigned community release trust model as a manual download.

## Build from source

On Windows x64 with the .NET Framework 4.x compiler and the .NET 10 SDK, install WiX CLI 4.0.6 and its matching UI extension. From the repository root run:

~~~powershell
dotnet tool install --global wix --version 4.0.6
wix extension add WixToolset.UI.wixext/4.0.6 -g
.\Build.ps1
~~~

The standalone MSI is written to dist. The src folder contains the C# setup app, tray monitor and icon renderer. The installer folder contains the WiX project. The MIT license covers the original code in this repository. The included v2.3 driver and WinUI control panel belong to the [upstream project](https://github.com/vitoplantamura/MagicTrackpad2ForWindows) and retain their GPLv2 license. The settings panel in panel/ is a GPLv2 adaptation with revised layout, motion, and English text. Corresponding sources and the license are installed under ThirdParty.
