# Privacy Browser

Version 1.0.2 is a Windows x64 testing release combining a native .NET/WPF control application,
an unpacked Mullvad Browser, and the `custom-proxy-build` Myst node from
`myst-lmprove`. It does not modify the Windows system proxy, DNS servers,
firewall, or route table.

## Status

The native integration and portable release packaging are implemented. Packet capture confirms browser payload
routing through the loopback backend and a Mysterium provider, no direct
browser TCP/DNS/UDP path, fail-closed behavior before launch and after backend
termination, and unaffected external `curl`/PowerShell traffic. Code signing,
reboot, and true standard-user validation gaps remain. See
`docs/VALIDATION_RESULTS.md`.

## Architecture

```text
PrivacyBrowser.exe (native WPF window)
  -> in-process BackendController
  -> starts myst.exe directly with its web UI disabled
  -> Myst TequilAPI control endpoint at 127.0.0.1:44050

Mullvad Browser (isolated profile)
  -> locked HTTP/HTTPS proxy policy at 127.0.0.1:4449
  -> myst-lmprove userspace WireGuard netstack
  -> selected Mysterium provider
  -> Internet

myst-lmprove control-plane connections -> direct Internet (allowed and recorded)
all other Windows applications          -> unchanged Windows network path
```

The application does not start the Electron `MysteriumVPN.exe` shell, does not
start or browse to `127.0.0.1:44051`, and does not host HTML for UI control.
WPF event handlers call an in-process controller; only the existing Myst daemon
control contract on `44050` remains. Port `4449` remains intentionally because
it is the browser's data-plane proxy, not a UI transport. See
`docs/NATIVE_UI_ARCHITECTURE.md` for the migration analysis and trust boundary.

The browser never receives a direct-fallback proxy configuration. If the
backend disappears, Firefox requests continue targeting the dead loopback
endpoint and fail closed. DNS prefetch, speculative connections, DNS-over-HTTPS,
and WebRTC are locked off. Mullvad Browser's anti-fingerprinting defaults are
retained and several core settings are locked on.

## Layout

Place unpacked dependencies under `vendor`:

```text
vendor/
  mullvad-browser/mullvadbrowser.exe
  myst-lmprove/resources/app.asar.unpacked/node_modules/@mysteriumnetwork/node/bin/win/x64/myst.exe
```

Other layouts can be supplied with launcher parameters.

Do not leave the current `myst-lmprove` CI installer installed. Testing found
that it creates an automatic `MysteriumVPNSupervisor` service even though the
proxy-mode runtime does not need it. Use a copied/unpacked application tree and
verify that the service is absent before testing. This packaging defect is an
upstream blocker, not hidden by this launcher.

## Build

Build the native Windows application with the .NET 8 SDK:

```powershell
.\Build.ps1
```

This publishes the WPF app to `app\PrivacyBrowser.exe`. Use
`-SelfContained` if the target machine does not have the .NET 8 Desktop Runtime.

The executable embeds the official multi-resolution Windows icon and reports
file/product version `1.0.2`. The native WPF window uses the matching embedded
PNG resource so Windows Imaging Component can decode it reliably at startup.

## Release package

Maintainers can build the complete self-contained Windows x64 bundle with:

```powershell
$env:MYST_RELEASE_TOKEN = "<token with read access to the pinned backend release>"
.\Package-Release.ps1
.\tests\Test-ReleasePackage.ps1
```

This creates `PrivacyBrowser-1.0.2-windows-x64-portable.zip`, its SHA-256
manifest, and the corresponding `myst-lmprove` source archive. The upstream
installers are downloaded at pinned hashes and extracted; they are never run.

## Run

From a source/development checkout:

```powershell
.\Start-PrivacyBrowser.ps1
```

For the release package, extract the ZIP to a user-writable directory and
double-click `PrivacyBrowser.exe` in the extracted top-level folder.

The userspace proxy path is intended to work without elevation, but the live
provider run used Administrator and the upstream installer requires elevation.
A genuine standard-user run remains a validation gap.

The application opens its own native window. Its overview shows backend,
identity, wallet, provider, and browser-readiness state at a glance. Use the
**Controls** button in the upper-right to:

- accept consumer terms and create/register an identity;
- view and refresh the identity's MYST balance;
- create a top-up through payment gateways reported by the Myst TequilAPI;
- discover, inspect, and select WireGuard providers;
- connect, disconnect, and launch the isolated browser; and
- see operation progress, results, prerequisite guidance, and friendly errors.

The browser launch button is enabled only after the backend reports
`CONNECTED`; launch also verifies that the expected backend process owns the
loopback proxy listener on port 4449. Payment checkout, provider availability,
and identity registration still depend on Mysterium's external services.

Use `-KeepBackendRunning` only for debugging; by default the native application
owns and cleans up the `myst.exe` process it started. Use `-SkipBackendLaunch`
only when deliberately adopting an already-running development backend on
`127.0.0.1:44050`.

## Install the policy

The launcher verifies and installs `config/policies.json` into the unpacked
browser's `distribution` directory before every launch. It refuses to replace
an unrelated policy file. The policy affects only this browser tree.

## Tests

```powershell
.\tests\Test-Configuration.ps1
.\tests\Test-Launcher.ps1
.\tests\Test-NativeArchitecture.ps1
.\tests\Test-ReleaseMetadata.ps1
.\tests\Test-Evidence.ps1
.\validation\Invoke-Validation.ps1 -ModifiedBrowserExe .\vendor\mullvad-browser\mullvadbrowser.exe
```

The validation command must run from an elevated prompt because `pktmon`
capture requires elevation. It writes timestamped evidence under `evidence/`.
Read `docs/VALIDATION_PLAN.md` before interpreting the result.

## Security boundaries

- This is not Tor and does not provide Tor circuits, relays, onion services,
  bridges, pluggable transports, or Tor's anonymity properties.
- The backend's direct discovery, identity, payment, monitoring, and provider
  negotiation traffic is allowed control-plane traffic.
- The Mysterium provider must be selected and connected before browsing.
- The launcher rejects non-loopback proxy settings and unexpected owners of
  port 4449.
- The native app binds no UI listener and never starts the legacy port 44051
  web server.
- The remaining port 44050 is the Myst daemon's existing loopback-only control
  API. It is explicitly accessed without the Windows/system HTTP proxy.
- A malicious or compromised backend remains inside the trust boundary.
