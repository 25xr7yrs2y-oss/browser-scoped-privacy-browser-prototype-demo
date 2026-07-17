# Version 1.0.2 testing release

Privacy Browser 1.0.2 is a Windows x64 testing release of the improved native controller.

## Improvements

- Reworks the native main page around connection, backend, identity, wallet, provider, and browser-readiness state.
- Adds a structured Privacy controls panel with visible operation progress, success, and failure feedback.
- Adds identity registration status and refresh controls.
- Adds MYST balance, payment guidance, balance refresh, and the TequilAPI-backed top-up entry.
- Restores reliable WireGuard provider discovery, displays up to 500 proposals, and supports native provider selection.
- Translates common backend failures into concise user-facing messages.
- Keeps the browser fail-closed until the app-owned Myst backend opens loopback proxy port 4449.
- Ships a self-contained .NET 8 Windows executable inside the complete portable bundle.

## Validation

- The Windows build, metadata, native architecture, backend-control, policy, icon, and evidence tests run in GitHub Actions.
- The build was deployed to Windows 10 22H2 and verified against the CI artifact hash.
- Startup, backend ownership, supplied identity detection, registration/balance display, terms acceptance, provider refresh and selection, top-up UI, shutdown, and restart were exercised on the test machine.
- In the restricted test network, Hermes timed out without Karing TUN and returned HTTP 200 with Karing TUN enabled.

## Known limitations

- This is a testing release and is intentionally marked as a GitHub pre-release.
- Password-protected exported Mysterium identities cannot yet be unlocked because the current native connection path supplies an empty passphrase. The UI reports `Unlock failed`; secure passphrase import/unlock support is planned.
- The password-protected test identity therefore did not reach a live provider session in the latest external-device run; port 4449, integrated-browser exit IP, and browser-only routing still require a follow-up test with an unlockable identity.
- Strict networks may require Karing TUN. Upstream SOCKS5 routing for every Myst control-plane and WireGuard transport path is not yet implemented.
- The package is portable and unsigned. Windows SmartScreen may display a warning.
