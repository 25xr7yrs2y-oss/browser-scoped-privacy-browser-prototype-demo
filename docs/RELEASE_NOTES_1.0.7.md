# Privacy Browser Prototype Demo v1.0.7

Privacy Browser 1.0.7 is a focused Windows x64 pre-release that introduces an
explicit, extensible payment-gateway adapter architecture while preserving the
security, timeout, diagnostics, connection-ID-4449, route-isolation, packaging,
and process-management protections from 1.0.4 through 1.0.6.

## Registered payment-gateway architecture

- Payment handling now routes through a case-exact adapter registry. Each
  adapter owns one canonical gateway name and one strict response contract.
- Discovery remains `GET /v2/payment-order-gateways?options_currency=MYST`.
  The UI exposes only gateways present in both that Myst response and the
  client registry.
- Order creation remains
  `POST /v2/identities/{identity}/{gateway}/payment-order`, with adapter support
  checked again immediately before the request.
- The order response must name the exact selected gateway. The validated target
  and created order are committed to UI state only after both creation and
  gateway-specific parsing succeed. Every new attempt first clears the prior
  target, order, result panel, and open-payment control.

The architecture can support additional gateways, but **CoinGate is the only
enabled adapter in 1.0.7** because it is the only gateway with a verified
response contract in this repository. This release does not claim support for
PayPal, debit cards, Stripe, Apple Pay, Google Pay, or any other payment method.
Unknown and case-mismatched gateway names fail closed.

## CoinGate target contract

The CoinGate adapter requires exactly one top-level, case-exact `paymentUrl`
string. It never recursively searches arbitrary `public_gateway_data` and does
not select unrelated or nested URLs. The target must be an absolute HTTPS URL
with a nonempty host, no user information, no whitespace or control characters,
no fragment (including an empty fragment delimiter), no explicit empty port,
and only the default HTTPS port.

## Preserved runtime and release behavior

- TequilAPI operation-specific deadlines, caller-cancellation handling,
  sanitized route/operation diagnostics, and indeterminate-connect
  reconciliation remain unchanged.
- The app-owned proxy continues to use connection ID 4449 for status,
  disconnect, reconciliation, readiness, and cleanup.
- Proxy mode remains isolated from Windows system routing and the supervisor;
  browser launch remains fail closed and bound to the owned loopback proxy.
- Packaging reuses the pinned v1.0.6 backend source commit, asset ID, embedded
  provenance, and SHA-256 without changing backend code.

## Validation scope and residual limitations

No local build or test was run while preparing this release. GitHub Actions are
the required build, unit, static, packaging, checksum, manifest, source-offer,
and release-publication gates.

This remains an unsigned GitHub pre-release. Clean-machine standard-user and
live provider/browser/payment validation are still required in a future
authorized cycle. TequilAPI remains unauthenticated loopback HTTP on port
44050, the browser profile remains persistent, and backend control-plane/P2P
reachability can still depend on the user's network. Windows SmartScreen may
warn about the unsigned executable.
