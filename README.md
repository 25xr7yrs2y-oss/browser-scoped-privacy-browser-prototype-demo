# Browser-Scoped Privacy Browser Prototype

This Windows-only prototype combines an unpacked Mullvad Browser with the
`custom-proxy-build` release of `myst-lmprove`. It does not modify the Windows
system proxy, DNS servers, firewall, or route table.

## Status

The integration layer is implemented and locally testable. A real provider
session and packet-capture run are still required before calling browser-only
routing validated. See `docs/VALIDATION_RESULTS.md` for the current evidence.

## Architecture

```text
Mullvad Browser (isolated profile)
  -> locked HTTP/HTTPS proxy policy at 127.0.0.1:4449
  -> myst-lmprove userspace WireGuard netstack
  -> selected Mysterium provider
  -> Internet

myst-lmprove control-plane connections -> direct Internet (allowed and recorded)
all other Windows applications          -> unchanged Windows network path
```

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
  myst-lmprove/MysteriumVPN.exe
```

Other layouts can be supplied with launcher parameters.

## Run

From a non-administrator PowerShell prompt:

```powershell
.\Start-PrivacyBrowser.ps1
```

The backend opens its loopback management page. Create/import an identity and
connect to a provider. The launcher waits until both the backend API reports
`CONNECTED` and port 4449 is owned by a process inside the configured backend
directory. Only then does it launch the browser.

Use `-BackendReadyTimeoutSeconds 0` to start the browser immediately for the
mandatory unavailable-at-launch fail-closed test. Use `-KeepBackendRunning`
only for debugging; the default owns and cleans up the backend it started.

## Install the policy

The launcher verifies and installs `config/policies.json` into the unpacked
browser's `distribution` directory before every launch. It refuses to replace
an unrelated policy file. The policy affects only this browser tree.

## Tests

```powershell
.\tests\Test-Configuration.ps1
.\tests\Test-Launcher.ps1
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
- A malicious or compromised backend remains inside the trust boundary.
