# Contributing

Keep the app small, understandable, and consistent with Windows 11. Prefer native controls and existing platform libraries. Changes to the driver or its signing are outside the companion app’s normal scope.

## Development

See [build instructions](docs/technical-details.md#build-from-source). The supported installer target is Windows 11 x64. Keep upstream credits and licenses intact. Avoid committing generated binaries, driver downloads, local settings, or build output.

For a pull request, explain the user-visible problem, what changes, and how it was verified. UI changes should include a screenshot. Check both USB and Bluetooth when changing device behavior; distinguish tested hardware from inferred compatibility.

## Release checklist

1. Update the application version and build with `Build.ps1`. It creates the MSI and `SHA256SUMS.txt` in `dist`.
2. Check fresh full and driver-only installs, upgrade with the app running, repair, and uninstall. Verify both states of **Launch Magic Trackpad**. Use a test machine or an isolated installer fixture where appropriate and state the scope of testing.
3. Check in-app updates, preserved preferences, and the battery indicator. Verify the published MSI's name matches the version expected by the updater.
4. Commit and push the source. Create a draft GitHub release with the version tag, matching MSI, checksum file, and short user-facing notes.
5. Verify the uploaded MSI size and SHA-256 against the local build before publishing as the latest release. Keep a usable latest release available while removing obsolete releases.
6. Update the README download link and verify it resolves. Retain source tags and license information when cleaning old release downloads.

Do not replace an existing version's MSI with a different build. Publish a new version for changes to the installed app or installer.
