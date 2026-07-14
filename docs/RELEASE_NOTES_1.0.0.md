# Version 1.0.0

Initial public release of Privacy Browser for Windows x64.

## Highlights

- Introduces the native .NET/WPF desktop control window.
- Removes the browser-based UI dependency on `127.0.0.1:44051`.
- Preserves the browser-scoped `127.0.0.1:4449` HTTP/CONNECT proxy architecture and locked Mullvad Browser policy.
- Adds the official application icon to the executable, native window, taskbar, and File Explorer presentation.
- Ships a self-contained portable Windows package with the pinned Mullvad Browser 15.0.14 runtime and the audited `myst-lmprove` node from commit `227d63b`.

## Known limitations

- This remains a prototype and has no signed installer; extract the portable ZIP and run `PrivacyBrowser.exe`.
- Windows SmartScreen may warn because the executable is not code-signed.
- Standard-user operation, reboot behavior, and installation/upgrade lifecycle behavior have not received full deployment validation.
- Real-world TUN-mode coexistence, external proxy compatibility, network-leak testing, and new packet capture were not performed for this release.
- The Myst daemon control API still uses loopback port `44050`; the removed `44051` port was only the legacy web UI.
