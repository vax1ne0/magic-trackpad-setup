# Magic Trackpad Setup for Windows

Set up an Apple Magic Trackpad on Windows 11 with a Precision Touchpad driver, a Windows-style control panel, and a battery indicator beside the clock.

Supports **Magic Trackpad Gen 3** (2021 Lightning model, A1535) and the **USB-C Magic Trackpad** (A3120) on Windows 11 x64.

**[Download the Windows x64 MSI](https://github.com/vax1ne0/magic-trackpad-setup/releases/latest)**

## Get started

1. Download and open **Magic-Trackpad-Setup-1.0.0-x64.msi**.
2. Open **Magic Trackpad Setup** from Start and select **Set up driver**. Allow about 60 MB for the first download.
3. Plug in the trackpad with a USB data cable, or select **Bluetooth settings**, unplug the cable, switch the trackpad off and on, and choose **Add device > Bluetooth**.
4. Choose **Open control panel** to adjust click feedback and finger-lift behavior. Windows Touchpad Settings controls taps, scrolling, and gestures.

The battery icon shows green above 50%, orange from 21–50%, and red at 20% or below. Hover for the percentage and connection. After it observes five percentage points of discharge and at least an hour of Bluetooth use, the monitor estimates hours remaining. Right-click the icon for refresh and optional launch at sign-in. If the icon is hidden, open the arrow beside the clock.

## Compatibility and trust

Designed for Windows 11 on Intel or AMD x64 PCs. The reference device was the USB-C Magic Trackpad A3120. Bluetooth, USB, battery reporting, and scrolling were used on the reference laptop. The v2.3 driver includes USB and Bluetooth hardware IDs for Gen 3. An A1535 owner also [reported success on Windows 11](https://github.com/vitoplantamura/MagicTrackpad2ForWindows/issues/20#issuecomment-4097637156). Local testing used the A3120. Windows on ARM has not been validated.

The community MSI and apps are unsigned. Windows can show an unknown-publisher prompt. The installer downloads the official [MagicTrackpad2ForWindows v2.3 release](https://github.com/vitoplantamura/MagicTrackpad2ForWindows/releases/tag/v2.3), checks the archive against its pinned SHA-256, then invokes Windows PnPUtil to install the Microsoft-signed driver. The community project has no affiliation with Apple or Microsoft.

Verify the MSI with PowerShell and compare the output to SHA256SUMS.txt on the same release:

~~~powershell
Get-FileHash .\Magic-Trackpad-Setup-1.0.0-x64.msi -Algorithm SHA256
~~~

The battery monitor runs elevated to use the driver's Bluetooth battery interface. Its readings and discharge history stay on the PC. Runtime estimates include idle time while connected. Pairing is manual.

## Remove or repair

Exit the battery monitor from its tray menu and close the control panel. Use **Settings > Apps > Installed apps > Magic Trackpad Setup for Windows** to uninstall. This removes the apps and their shortcuts and leaves the trackpad driver installed. To repair the Windows apps, rerun the MSI. To check the driver package again, select **Set up driver** in the app.

## Build from source

On Windows x64 with the .NET Framework 4.x compiler and a modern .NET SDK, install WiX CLI 4.0.6 and its matching UI extension. From the repository root run:

~~~powershell
dotnet tool install --global wix --version 4.0.6
wix extension add WixToolset.UI.wixext/4.0.6 -g
.\Build.ps1
~~~

The standalone MSI is written to dist. The src folder contains the C# setup app, tray monitor and icon renderer. The installer folder contains the WiX project. The MIT license covers the original code in this repository. The separately downloaded driver and WinUI control panel belong to the [upstream project](https://github.com/vitoplantamura/MagicTrackpad2ForWindows) and retain its license.
