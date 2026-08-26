# Privacy Browser Prototype Demo v1.0.4

Privacy Browser 1.0.4 is a Windows x64 testing release that incorporates the
product-security hardening completed after v1.0.3. It retains browser-scoped
Mysterium routing without changing the Windows system proxy, DNS, firewall, or
route table.

## Main improvements since v1.0.3

- Hardens payment-target parsing with gateway-specific response fields, HTTPS
  enforcement, and rejection of credentials, fragments, explicit empty ports,
  and non-default ports.
- Clears previously validated payment targets before every new payment-order
  attempt and adds dedicated parser regression tests.
- Refactors the controller into focused Home, Identity, Wallet, Connection, and
  Browser & Diagnostics pages while preserving shared backend and browser state.
- Adds contextual progress, success, prerequisite, and failure feedback with
  stable support codes and regression coverage.
- Extends Windows CI to validate the new navigation architecture in addition to
  the existing product, security, packaging, and metadata checks.

## Packaging and testing

- Publishes a complete self-contained Windows x64 portable ZIP, the corresponding
  `myst-lmprove` source archive, and a SHA-256 checksum file.
- Portable binaries remain unsigned, so Windows SmartScreen may display a warning.
- This remains a testing release and is published as a GitHub pre-release.

## Known limitations

- Myst TequilAPI remains unauthenticated loopback HTTP on fixed port 44050.
- The persistent browser profile does not yet offer an ephemeral mode or a
  verified reset/clear workflow.
- Strict networks may require an external tunnel; not every Myst control-plane
  or WireGuard transport path is upstream-proxyable.
- Clean-machine live provider/payment and packet-capture validation remains
  recommended for this exact release artifact.
