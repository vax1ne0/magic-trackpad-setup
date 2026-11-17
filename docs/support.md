# Help with Magic Trackpad

## Before installing

Use the MSI from the [latest release](https://github.com/vax1ne0/magic-trackpad-setup/releases/latest). It includes the driver and app; no separate driver download is needed. Choose **Driver and app (recommended)** for click settings and the taskbar battery indicator, or **Driver only** for Windows gestures without the companion app.

Supported PCs run Windows 11 on an Intel or AMD x64 processor. Supported trackpads are the 2024 USB-C A3120 and the 2021 Lightning Gen 3 A1535. Windows on ARM is not supported.

## Connecting

For USB, use a cable that carries data, not just power. For Bluetooth, unplug the cable, turn on the trackpad, and select it in Windows Bluetooth pairing. If it is connected to another computer, disconnect it there first. Pairing takes place in Windows, not in this app.

## Battery indicator

The icon appears in the taskbar notification area while the trackpad is connected. Windows may put it in the hidden-icons menu beside the clock. You can move it into the visible notification area using Windows taskbar settings.

Remaining hours are an estimate learned from Bluetooth use, including connected idle time. A new installation needs discharge history before it can show a useful estimate. USB power and changes in usage affect the estimate.

**Open at startup** controls whether the indicator starts when you sign in. **Launch Magic Trackpad** on the installer’s final page controls only whether the app opens when setup finishes.

## Updates and setup warnings

Use **App settings → Check for updates** or download the latest MSI. Save pending settings first. Updates retain settings and battery history.

The community installer and app are unsigned, so Windows may show an unknown-publisher or SmartScreen warning. The bundled driver is Microsoft-signed. Signing the driver does not sign this installer. Download only from this repository’s releases; do not disable Windows security protections.

If setup fails, note the exact message and report it using the link below. Include the app version you were upgrading from and whether you chose driver-only or the full app.

## Report a problem

Open [Report a problem](https://github.com/vax1ne0/magic-trackpad-setup/issues/new?template=bug_report.yml). Include:

- What you expected and what happened.
- Steps that reproduce the problem.
- Whether you were using Bluetooth or a cable.
- The report from **Copy device details**, beside **Help** in the app.

Review screenshots and logs before posting. Remove names, personal file paths, Bluetooth addresses, and other private information. GitHub issues are public.

For removal behavior, repair, checksums, or building from source, see [technical details](technical-details.md).
