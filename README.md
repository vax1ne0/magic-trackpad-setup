# Magic Trackpad for Windows

Use your Apple Magic Trackpad on Windows 11. Scroll with two fingers, adjust how clicks feel, and see your battery status in your taskbar.

Built on Vito Plantamura’s [MagicTrackpad2ForWindows](https://github.com/vitoplantamura/MagicTrackpad2ForWindows) driver and control panel.

![Magic Trackpad settings on Windows 11 showing 100% battery over Bluetooth, click feel, and pointer settings](docs/magic-trackpad-settings.png)

## Get started

1. **[Download the Windows installer (1.6.2)](https://github.com/vax1ne0/magic-trackpad-setup/releases/download/v1.6.2/Magic-Trackpad-Setup-1.6.2-x64.msi)** and run it.
2. **Connect your trackpad.** Plug it in with a USB data cable, or unplug the cable and pair it under **Settings → Bluetooth & devices → Add device → Magic Trackpad**.
3. **Enjoy your full Magic Trackpad experience.** Try a two-finger scroll or pinch to zoom.

## Customization

Open **Magic Trackpad** from the Start menu. Under **Click feel**, choose a light, medium, or firm click. Turn on **Use a quieter click** for less sound, or choose the strongest feedback for a louder, more pronounced click. Prefer touch only? Turn off the physical click and its haptic feedback. To click with a light tap, enable tap-to-click under **Windows gestures**.

You can also adjust when the pointer stops as you lift a finger, ignore accidental palm touches, or change how dragging works. Under **App settings**, turn on **Open at startup** to see your battery status in the taskbar when you sign in.

The taskbar battery icon changes color as the charge drops. Hover over it for the exact percentage. After a few percentage points of Bluetooth use, it also estimates how many hours are left. If the icon is hidden, click the arrow beside the clock.

## Features

- **Choose what to install.** The recommended option includes the driver, control panel, and taskbar battery indicator. Choose **Driver only** if Windows touchpad settings are all you need.
- **Adjust the release feel.** Under **Click feel**, the feedback when you lift your finger can match the press or have its own light, medium, or firm setting.
- **Get a low-battery warning.** Under **App settings**, choose whether to receive a notification at 20% battery on Bluetooth. The taskbar icon hides when the trackpad disconnects and returns when it reports its charge again.
- **Check for updates.** In the full installation, open **App settings → Check for updates**. When a newer release is available, it downloads in the background. Choose **Restart and update** to apply it silently and reopen the app, or **Later** to keep using the app.
- **Use the panel in Arabic.** Choose Arabic under **App settings** for a right-to-left layout. Settings rows also highlight across their full width when you hover.

## Compatibility

Supports Windows 11 on Intel and AMD PCs with either the **2024 USB-C Magic Trackpad (A3120)** or the **2021 Lightning Magic Trackpad Gen 3 (A1535)**. The USB-C model was tested over both USB and Bluetooth.

Windows may show an unknown-publisher prompt because this community installer is unsigned. The trackpad driver is Microsoft-signed.

## Credits

The signed driver and original settings panel come from Vito Plantamura’s [MagicTrackpad2ForWindows](https://github.com/vitoplantamura/MagicTrackpad2ForWindows), a fork of imbushuo’s [mac-precision-touchpad](https://github.com/imbushuo/mac-precision-touchpad). [Full upstream credits](https://github.com/vitoplantamura/MagicTrackpad2ForWindows#credits)

The driver and adapted settings panel retain their GPLv2 license. The installer includes their source and license in the `ThirdParty` folder. [Licensing and build details](docs/technical-details.md)

## What this project adds

- One Windows installer for the driver, settings panel, and battery monitor.
- A taskbar battery icon with the charge percentage and an estimate of hours left based on Bluetooth use.
- An option to start the battery monitor at sign-in.
- A reorganized settings panel with clearer labels.

[Release notes](https://github.com/vax1ne0/magic-trackpad-setup/releases) · [Technical details and build instructions](docs/technical-details.md)
