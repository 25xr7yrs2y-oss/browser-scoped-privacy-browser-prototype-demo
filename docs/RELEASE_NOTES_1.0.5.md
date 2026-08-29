# Privacy Browser Prototype Demo v1.0.5

Privacy Browser 1.0.5 is a focused Windows x64 testing release that corrects
TequilAPI timeout handling while preserving browser-scoped Mysterium routing,
loopback-only controls, process ownership checks, and fail-closed browser policy.

## Timeout and connection-state fixes

- Removes the shared 15-second `HttpClient.Timeout` that silently preempted the
  intended 75-second provider-connect deadline. The shared client is now
  unbounded and every request receives one explicit operation deadline.
- Uses reviewed budgets of 2 seconds per health probe, 15 seconds for ordinary
  local TequilAPI operations, 30 seconds for provider discovery, 75 seconds for
  provider connection, and 8 seconds for graceful stop.
- Makes the 30-second backend-readiness window one absolute deadline. Probe and
  retry delays are capped by the remaining time instead of accumulating beyond
  the advertised startup window.
- Distinguishes caller cancellation, operation deadline expiry, loopback
  transport failure, backend HTTP rejection, and malformed response handling.
- Adds operation-specific, secret-free timeout diagnostics. A timeout no longer
  automatically tells the user that general Internet connectivity is the cause.
- After a provider-connect deadline, performs one bounded `GET /connection`
  reconciliation. `CONNECTING` blocks duplicate PUTs, `CONNECTED` is accepted
  only after the app-owned loopback proxy check, and a final failed/not-connected
  state is surfaced without provider or identity details. No unbounded polling
  or automatic duplicate connection attempt is introduced.

## Validation

- Adds deterministic fake-TequilAPI tests covering operations that finish after
  the ordinary budget but within discovery/connect budgets, deadline expiry,
  caller cancellation, readiness absolute timing, sanitized diagnostics,
  malformed/HTTP error separation, and connect reconciliation for `CONNECTING`,
  `CONNECTED`, and failed/not-connected outcomes.
- Windows CI builds and smoke-starts the WPF application, runs the new deadline
  suite and the existing PowerShell/runtime security checks, publishes a
  self-contained QA build, and validates release metadata.
- The tag workflow builds and verifies the complete portable ZIP, corresponding
  pinned Myst source archive, and SHA-256 checksum file before publication.

No live provider connection, identity unlock, passphrase use, payment order,
top-up, transfer, or purchase was performed for this release. Provider behavior
is exercised only through deterministic local test handlers.

## Distribution status and known limitations

- This is an unsigned GitHub pre-release. Windows SmartScreen may display a warning.
- Myst TequilAPI remains unauthenticated loopback HTTP on fixed port 44050.
- The persistent browser profile does not yet offer an ephemeral mode or a
  verified reset/clear workflow.
- Strict networks may require an external tunnel; not every Myst control-plane
  or WireGuard transport path is upstream-proxyable.
- Clean-machine standard-user and live provider/payment validation remains
  recommended for a future, separately authorized test cycle.
