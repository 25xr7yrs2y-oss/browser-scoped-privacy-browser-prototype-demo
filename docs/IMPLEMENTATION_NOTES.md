# Implementation Notes

## Base selection

Mullvad Browser is the base because it already removes normal Tor-network
operation while retaining Tor Browser-derived anti-fingerprinting hardening.
The prototype uses an upstream unpacked Windows build and adds only a scoped
enterprise policy, isolated profile, backend lifecycle, and validation tools.
This avoids maintaining a full Firefox fork before the routing architecture is
proven.

Exact source checked: branch `mullvad-browser-140.12.0esr-15.0-1`, commit
`ba34b2b615afc4dff62f5b9db4be8f04e32f2602`. Source findings:

- `001-base-profile.js` enables RFP, canvas randomization, letterboxing,
  first-party isolation, disabled telemetry, disabled predictor/prefetch/DNS
  prefetch, and defense-in-depth WebRTC ICE restrictions.
- `000-mullvad-browser.js` enables Mullvad DoH with `network.trr.mode=3`; this
  prototype deliberately overrides it with locked mode 5 because a browser DoH
  connection must not bypass the local HTTP proxy model.
- Mullvad updater URLs and automatic-update defaults remain in source; the
  prototype's `DisableAppUpdate` policy prevents an unvalidated version from
  replacing the tested build.
- Enterprise policy source and `test_proxy.js` confirm manual HTTP/SSL address,
  all-protocol HTTP proxy, passthrough, and locked proxy behavior used here.
- No Tor daemon/control-port source path was present in the selected sparse
  runtime areas; Tor issue references remain in inherited hardening comments.

## Backend source audit

Audited branch: `25xr7yrs2y-oss/myst-lmprove@227d63b`, branch
`custom-proxy-build`.

- Desktop entry: Electron `src/main/index.tsx` starts a loopback web server and
  Myst node.
- Node entry: `src/main/node/mysteriumNode.ts` launches `myst.exe` with
  `--usermode --proxymode --proxy.bind.address=127.0.0.1 --consumer`.
- Protocol: `custom-node/services/wireguard/endpoint/proxyclient/handler.go`
  implements ordinary HTTP forwarding and HTTP CONNECT. No SOCKS server was
  found in the proxyclient implementation despite the README saying SOCKS/HTTP.
- Bind: proxyclient constructs `127.0.0.1:4449` from the explicit bind flag and
  connection option.
- Tunnel model: proxy mode selects `proxyclient.New()`, which uses WireGuard's
  userspace netstack (`CreateNetTUN`) and an HTTP server. It does not select the
  Wintun/kernel path.
- System routing: proxy mode bypasses tunnel reconnect/IP checks; Windows route
  add/delete/default-route helpers are no-ops in this fork.
- Privilege: the selected proxyclient path is userspace and is intended to run
  without administrator rights. This still needs real non-admin validation.
- Control plane: discovery, identity, registration/payment, monitoring, and
  provider negotiation use the backend's direct HTTP/P2P clients. These direct
  connections are allowed, must be attributed to backend processes, and are
  not browser payload.
- Lifecycle: Electron owns the Myst child and its quit path calls node stop.
  The wrapper additionally calls `/api/node/stop` and terminates only the
  backend process tree that it started.

### Packaging discrepancy found in live testing

The successful CI artifact for commit `227d63b` installs an automatic Windows
service named `MysteriumVPNSupervisor`. This contradicts the fork README's
"No supervisor install" claim. The tested service executable was
`resources/app.asar.unpacked/node_modules/@mysteriumnetwork/node/bin/win/x64/myst_supervisor.exe
-winservice`. The application runtime still selected the expected userspace
proxyclient path, but the installer is not acceptable for a browser-scoped
package as-is. The test installation was uninstalled and the service was
verified absent.

## Tor-specific classification

Mullvad Browser already omits normal Tor daemon bootstrap, circuit UX, bridges,
pluggable transports, onion services, and Tor control-port operation. The
prototype adds none of them.

- Safe to remove/absent: Tor daemon startup, bootstrap UI, bridges/transports,
  onion services, circuit display, onion-location, Tor New Identity semantics.
- Replaced: Tor SOCKS routing is replaced by locked HTTP/CONNECT proxy policy;
  Tor DNS isolation is replaced by hostname-bearing CONNECT plus disabled
  browser DNS/DoH; Tor network fail-closed behavior is replaced by a fixed
  loopback proxy with no direct fallback.
- Dangerous to remove: anti-fingerprinting defaults, RFP, letterboxing,
  first-party isolation, telemetry/background reductions, safe external-app and
  download handling. These remain inherited from Mullvad and core items are
  locked by policy.

## Known limitations

- The wrapper is a prototype integration, not a rebuilt branded Firefox fork.
- HTTP CONNECT exposes destination hostnames to the local backend by design.
- Disabling WebRTC is stronger than proxying it and breaks WebRTC applications.
- OCSP, captive portal, extension/update, and other browser background paths
  require packet validation against the exact Mullvad release.
- Extension behavior and policy compatibility must be retested on every browser
  update. App updates are disabled so an update cannot silently replace this
  policy/build combination.
- Backend claims about SOCKS support were not confirmed by implementation;
  this integration uses HTTP/CONNECT only.
- Provider availability, identity registration, payment state, and backend
  control-plane behavior are external dependencies.
- Creating a fresh identity during the first run ended as `Unregistered` with
  backend log evidence `no contract code at given address`. A later run used a
  separately supplied registered identity and completed provider-path, DNS,
  WebRTC, and crash-after-connect validation. No identity key material or
  password was retained in evidence or the repository.
