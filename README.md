# Magic Trackpad for Windows

Use your Apple Magic Trackpad on Windows 11. Scroll with two fingers, adjust how clicks feel, and see your battery status in your taskbar.

Built on Vito Plantamura’s [MagicTrackpad2ForWindows](https://github.com/vitoplantamura/MagicTrackpad2ForWindows) driver and control panel.

![Magic Trackpad settings on Windows 11 showing 100% battery over Bluetooth, click feel, and pointer settings](docs/magic-trackpad-settings.png)

## Get started

1. **[Download the Windows installer (1.6.7)](https://github.com/vax1ne0/magic-trackpad-setup/releases/download/v1.6.7/Magic-Trackpad-Setup-1.6.7-x64.msi)** and run it.
2. **Connect your trackpad.** Plug it in with a USB data cable, or unplug the cable and pair it under **Settings → Bluetooth & devices → Add device → Magic Trackpad**.
3. **Enjoy your full Magic Trackpad experience.** Try a two-finger scroll or pinch to zoom.

## Features

- **Installer choices.** The recommended setup includes the driver, control panel, and taskbar battery indicator. Choose **Driver only** if Windows touchpad settings are all you need. Leave **Launch Magic Trackpad** checked on the final page to open the app when setup finishes.
- **Taskbar battery status.** Hover over the color-coded icon for the charge percentage and estimated hours left. The estimate learns from Bluetooth use. The icon hides when the trackpad disconnects. Under **App settings**, you can enable a 20% Bluetooth battery alert and turn on **Open at startup** to show the indicator when you sign in.
- **In-app updates.** Open **App settings → Check for updates**. A compact progress bar shows the percentage downloaded, megabytes left, and speed in MB/s. When the download finishes, choose **Restart and update** or **Update on next launch**. The second option saves the download and applies it the next time you open Magic Trackpad, or when you sign in with **Open at startup** enabled.
- **Language options.** Choose your language under **App settings**, including Arabic with a right-to-left layout.

## Customization

Open **Magic Trackpad** from the Start menu. Under **Click feel**, choose a light, medium, or firm press. The feedback when you lift your finger can match the press or have its own strength. Enable **Use a quieter click** for less sound, or choose the strongest feedback for a louder click.

Prefer touch only? Turn off the physical click and its haptic feedback, then enable tap-to-click under **Windows gestures**. Under **Pointer lift-off** and **Touch and pointer settings**, you can adjust when the pointer stops, ignore accidental palm touches, and change how dragging works.

Use **Copy device details** beside **Help** when reporting a problem. Under **App settings**, **Restore trackpad defaults** lets you review the defaults before saving.

## Compatibility

Supports Windows 11 on Intel and AMD PCs with either the **2024 USB-C Magic Trackpad (A3120)** or the **2021 Lightning Magic Trackpad Gen 3 (A1535)**. The USB-C model was tested over both USB and Bluetooth.

Windows may show an unknown-publisher prompt because this community installer is unsigned. The trackpad driver is Microsoft-signed.

## Help

[Get help](docs/support.md) · [Report a problem](https://github.com/vax1ne0/magic-trackpad-setup/issues/new?template=bug_report.yml) · [Suggest a feature](https://github.com/vax1ne0/magic-trackpad-setup/issues/new?template=feature_request.yml)

## Credits

The signed driver and original settings panel come from Vito Plantamura’s [MagicTrackpad2ForWindows](https://github.com/vitoplantamura/MagicTrackpad2ForWindows), a fork of imbushuo’s [mac-precision-touchpad](https://github.com/imbushuo/mac-precision-touchpad). [Full upstream credits](https://github.com/vitoplantamura/MagicTrackpad2ForWindows#credits)

The driver and adapted settings panel retain their GPLv2 license. The installer includes their source and license in the `ThirdParty` folder. [Licensing and build details](docs/technical-details.md)

[Release notes](https://github.com/vax1ne0/magic-trackpad-setup/releases) · [Technical details and build instructions](docs/technical-details.md)
