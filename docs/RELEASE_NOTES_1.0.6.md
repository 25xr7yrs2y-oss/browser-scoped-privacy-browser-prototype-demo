# Privacy Browser Prototype Demo v1.0.6

Privacy Browser 1.0.6 is a focused Windows x64 pre-release that fixes two
connection-blocking defects while preserving the 1.0.5 timeout, diagnostic,
browser-scoping, ownership, integrity, and fail-closed protections.

## Proxy-mode route and supervisor isolation

- The pinned Myst backend now skips peer system-route exclusion at the P2P
  semantic call site whenever proxy mode is active.
- Proxy mode also selects a no-op routing implementation as defense in depth.
  The supported `--usermode --proxymode` launch therefore does not discover or
  modify Windows routes and does not access the supervisor named pipe.
- Actual system-tunnel modes retain peer route protection. Their initial
  gateway discovery is now cancelable, retries at most five times with
  exponential backoff, and returns a specific error instead of spinning in an
  unbounded hot loop. Route initialization errors are propagated to callers.

## App-owned connection ID 4449

- Myst keys proxy connections by `proxy_port`. The controller already creates
  its proxy with port/ID 4449; status reads, disconnects, timeout
  reconciliation, snapshot readiness, and cleanup now all derive
  `connection?id=4449` from the same `ProxyPort` constant.
- Regression coverage creates the 4449 proxy connection and verifies that
  every status and delete request uses ID 4449, never the endpoint default ID 0.

## Preserved timeout and diagnostic behavior

- The shared `HttpClient` remains unbounded and every TequilAPI operation keeps
  its reviewed explicit deadline: 30 seconds for discovery and 75 seconds for
  provider connect, with shorter budgets for ordinary work and health probes.
- Caller cancellation remains distinct from deadline expiry. An indeterminate
  provider connection is reconciled through the ID-4449 status path before any
  duplicate PUT is allowed.
- Diagnostic records remain operation- and route-labeled and do not include
  identity IDs, provider IDs, passphrases, request bodies, or proxy credentials.

## Backend provenance

- The package pins backend source commit
  `7944a4c634834aac10a4e8e49934e326ac3f0e7a` and release asset ID `537461102`.
- The trusted backend tag workflow built the binary, ran the route regression
  tests, injected and verified the release identifier and exact source commit
  in `myst.exe --version`, and published a provenance JSON record.
- The packaged backend SHA-256 is
  `5b761c82022d77bd1229ebb9e5e7bc35353a7e3c6b842e33967a643d181c25b2`.

## Validation scope and residual limitations

Automated Windows build, unit, static, packaging, checksum, manifest, source
offer, and release verification are the release gates. No live Windows device,
provider, identity, wallet, browser, Karing, LAN, or external network testing
was performed for 1.0.6.

This remains an unsigned GitHub pre-release. Clean-machine standard-user and
live provider/browser validation are still required in a future authorized
cycle. TequilAPI remains unauthenticated loopback HTTP on port 44050, the
browser profile remains persistent, and backend control-plane/P2P reachability
can still depend on the user's network. Windows SmartScreen may warn about the
unsigned executable.
