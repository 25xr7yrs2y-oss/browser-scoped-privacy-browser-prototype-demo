# Privacy Browser Prototype Demo v1.0.3

Privacy Browser 1.0.3 is a Windows x64 testing release focused on protected
identity support, reliable privacy readiness, backend recovery, and portable
bundle integrity. It retains browser-scoped Mysterium routing without changing
the Windows system proxy, DNS, firewall, or route table.

## Main improvements since v1.0.2

- Adds native passphrase-protected identity creation, encrypted identity import,
  unlock, and credential retry.
- Models all Myst identities, adds explicit identity selection, and persists
  only the selected public identity address.
- Adds a unified privacy-readiness evaluator shared by the UI and browser
  launcher. “Ready” now requires a connected provider, the expected locked
  policy, and an app-owned loopback proxy listener.
- Adds backend lifecycle states and an in-app restart action after startup
  failure or an unexpected backend exit.
- Tracks the isolated browser process, refuses duplicate profile launches, and
  warns before controller shutdown leaves a running browser fail-closed.
- Adds provider search, accurate readiness visuals, retryable credential errors,
  and copyable user activity.
- Adds single-controller enforcement to prevent fixed-port and profile-lock
  collisions.
- Adds `bundle-manifest.json` with release identity, file sizes, and SHA-256 for
  the controller, browser, policy, and Myst backend. Validation occurs before
  backend startup in the portable layout.

## Important bug and security fixes

- Removes every hard-coded empty passphrase from identity create, register, and
  provider-connect operations.
- Requires a fully registered identity before enabling provider connection;
  registration-in-progress is no longer treated as connection-ready.
- Prevents browser launch with `--skip-backend-launch`, because an adopted
  loopback proxy cannot be proven to belong to the intended Myst process.
- Prevents the UI from claiming privacy is active before proxy ownership and
  policy checks succeed.
- Replaces unknown raw backend diagnostics with concise messages and stable
  support codes; unlock and import failures have specific retry guidance.
- Detects incomplete, stale, mixed, or runtime-overridden critical portable
  components before they enter the trusted process chain.

## Architecture and testing

- Keeps the native WPF/in-process controller architecture introduced before
  v1.0.2; the removed localhost HTML UI on port 44051 is not restored.
- Keeps the browser locked to `127.0.0.1:4449` with no direct proxy fallback.
- Adds product-hardening regression coverage to the Windows workflow and
  extends release-package verification to validate the component manifest.
- Publishes both the complete self-contained portable ZIP and corresponding
  `myst-lmprove` source archive with a SHA-256 checksum file.

## Known limitations

- This remains a testing release and is published as a GitHub pre-release.
- The portable binaries are unsigned; Windows SmartScreen may display a
  warning. The in-bundle manifest detects component mismatch but is not a code
  signature.
- Myst TequilAPI remains unauthenticated loopback HTTP on fixed port 44050. A
  per-user named pipe or per-launch capability requires upstream backend work.
- The persistent browser profile does not yet offer an ephemeral mode or a
  verified reset/clear workflow.
- Identity export, removal, labels, backup guidance, and recovery remain future
  work.
- Strict networks may require an external tunnel. Complete upstream proxying of
  every Myst control-plane and WireGuard transport path is not implemented.
- A new clean-machine live provider/payment and packet-capture validation is
  still recommended for this exact release artifact.
