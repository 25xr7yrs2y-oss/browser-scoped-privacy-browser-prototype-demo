# Browser-Scoped Privacy Browser Prototype

This Windows-only prototype combines a native .NET/WPF control application,
an unpacked Mullvad Browser, and the `custom-proxy-build` Myst node from
`myst-lmprove`. It does not modify the Windows system proxy, DNS servers,
firewall, or route table.

## Status

The integration layer is implemented. Packet capture confirms browser payload
routing through the loopback backend and a Mysterium provider, no direct
browser TCP/DNS/UDP path, fail-closed behavior before launch and after backend
termination, and unaffected external `curl`/PowerShell traffic. Packaging,
reboot, and true standard-user validation gaps remain. See
`docs/VALIDATION_RESULTS.md`.

## Architecture

```text
PrivacyBrowser.App.exe (native WPF window)
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

This publishes the WPF app to `app\PrivacyBrowser.App.exe`. Use
`-SelfContained` if the target machine does not have the .NET 8 Desktop Runtime.

## Run

From a PowerShell prompt in a user-writable unpacked bundle:

```powershell
.\Start-PrivacyBrowser.ps1
```

The userspace proxy path is intended to work without elevation, but the live
provider run used Administrator and the upstream installer requires elevation.
A genuine standard-user run remains a validation gap.

The application opens its own native window. Use the **Controls** button in the
upper-right to create/register an identity, load providers, connect, disconnect,
and launch the browser. The browser launch button is enabled only after the
backend reports `CONNECTED`; launch also verifies that the expected backend
process owns the loopback proxy listener on port 4449.

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
