# Compatibility and technical details

Designed for Windows 11 on Intel or AMD x64 PCs. The reference device was the 2024 USB-C Magic Trackpad (A3120). Bluetooth, USB, battery reporting, and scrolling were used on the reference laptop. The v2.3 driver includes USB and Bluetooth hardware IDs for Gen 3. An A1535 owner also [reported success on Windows 11](https://github.com/vitoplantamura/MagicTrackpad2ForWindows/issues/20#issuecomment-4097637156). Local testing used the A3120. Windows on ARM has not been validated.

The community MSI and apps are unsigned. Windows can show an unknown-publisher prompt. The build downloads the official [MagicTrackpad2ForWindows v2.3 release](https://github.com/vitoplantamura/MagicTrackpad2ForWindows/releases/tag/v2.3), checks the archive against its pinned SHA-256, and verifies the Microsoft catalog signature. The MSI includes those files and invokes Windows PnPUtil to install the Microsoft-signed driver. The community project has no affiliation with Apple or Microsoft.

Verify the MSI with PowerShell and compare the output to SHA256SUMS.txt on the same release:

~~~powershell
Get-FileHash .\Magic-Trackpad-Setup-1.3.0-x64.msi -Algorithm SHA256
~~~

The battery monitor runs elevated to use the driver's Bluetooth battery interface. Its readings and discharge history stay on the PC. Runtime estimates include idle time while connected. Pairing is manual.

## Remove or repair

Exit the battery monitor from its tray menu and close the control panel. Use **Settings > Apps > Installed apps > Magic Trackpad Setup for Windows** to uninstall. This removes the apps and their shortcuts and leaves the trackpad driver installed. To repair the installation, use Windows Installer repair.

## Build from source

On Windows x64 with the .NET Framework 4.x compiler and a .NET SDK supporting the .NET 8 Windows target, install WiX CLI 4.0.6 and its matching UI extension. From the repository root run:

~~~powershell
dotnet tool install --global wix --version 4.0.6
wix extension add WixToolset.UI.wixext/4.0.6 -g
.\Build.ps1
~~~

The standalone MSI is written to dist. The src folder contains the C# setup app, tray monitor and icon renderer. The installer folder contains the WiX project. The MIT license covers the original code in this repository. The included v2.3 driver and WinUI control panel belong to the [upstream project](https://github.com/vitoplantamura/MagicTrackpad2ForWindows) and retain their GPLv2 license. The settings panel in panel/ is a GPLv2 adaptation with revised layout, motion, and English text. Corresponding sources and the license are installed under ThirdParty.
