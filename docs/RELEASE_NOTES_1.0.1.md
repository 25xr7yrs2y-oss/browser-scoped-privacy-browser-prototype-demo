# Version 1.0.1

Maintenance release of Privacy Browser for Windows x64.

## Fixes

- Fixes the application startup failure reported as a WPF `Baml2006.TypeConverterMarkupExtension` exception.
- Regenerates the multi-resolution executable ICO with Windows-compatible DIB frames and uses the matching embedded PNG for the native window icon.
- Adds a Windows decoder regression test for the exact WPF startup resource.
- Writes full exception details to `state/startup-error.log` if a future startup failure occurs and the package directory is writable.
- Improves portable release startup stability without changing the native desktop UI or browser-scoped proxy architecture.

## Known limitations

- This remains a prototype and has no signed installer; extract the portable ZIP and run `PrivacyBrowser.exe`.
- Windows SmartScreen may warn because the executable is not code-signed.
- Real-world TUN-mode coexistence, external proxy compatibility, network-leak testing, and new packet capture were not performed for this release.
- The Myst daemon control API still uses loopback port `44050`; the removed `44051` port was only the legacy web UI.
