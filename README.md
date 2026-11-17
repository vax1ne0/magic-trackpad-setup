# Magic Trackpad Setup for Windows

Set up an Apple Magic Trackpad on Windows 11 with a Precision Touchpad driver, a Windows-style control panel, and a battery indicator beside the clock.

Supports **Magic Trackpad Gen 3** (2021 Lightning model, A1535) and the **2024 USB-C Magic Trackpad** (A3120) on Windows 11 x64.

**[Download the Windows x64 MSI](https://github.com/vax1ne0/magic-trackpad-setup/releases/latest)**

## Get started

1. Download and run **Magic-Trackpad-Setup-1.2.0-x64.msi**. The MSI installs the driver and control panel offline and starts the battery monitor for the installing user. Windows may request administrator approval.
2. Connect the trackpad with a USB data cable, or pair it through **Settings > Bluetooth & devices > Add device > Bluetooth**. For wireless pairing, unplug the cable and switch the trackpad off and on.
3. Open **Magic Trackpad** in Start whenever you want to adjust click strength under **Click feel**, fine-tune **Pointer lift-off**, or choose **Windows gestures**. Select **Save changes** after editing trackpad settings. The tray monitor starts at sign-in by default. Turn that off in its right-click menu.

The battery icon shows green above 50%, orange from 21–50%, and red at 20% or below. Hover for the percentage and connection. After it observes five percentage points of discharge and at least an hour of Bluetooth use, the monitor estimates hours remaining. Right-click the icon for refresh and optional launch at sign-in. If the icon is hidden, open the arrow beside the clock.

## Compatibility and trust

Designed for Windows 11 on Intel or AMD x64 PCs. The reference device was the 2024 USB-C Magic Trackpad (A3120). Bluetooth, USB, battery reporting, and scrolling were used on the reference laptop. The v2.3 driver includes USB and Bluetooth hardware IDs for Gen 3. An A1535 owner also [reported success on Windows 11](https://github.com/vitoplantamura/MagicTrackpad2ForWindows/issues/20#issuecomment-4097637156). Local testing used the A3120. Windows on ARM has not been validated.

The community MSI and apps are unsigned. Windows can show an unknown-publisher prompt. The build downloads the official [MagicTrackpad2ForWindows v2.3 release](https://github.com/vitoplantamura/MagicTrackpad2ForWindows/releases/tag/v2.3), checks the archive against its pinned SHA-256, and verifies the Microsoft catalog signature. The MSI includes those files and invokes Windows PnPUtil to install the Microsoft-signed driver. The community project has no affiliation with Apple or Microsoft.

Verify the MSI with PowerShell and compare the output to SHA256SUMS.txt on the same release:

~~~powershell
Get-FileHash .\Magic-Trackpad-Setup-1.2.0-x64.msi -Algorithm SHA256
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
