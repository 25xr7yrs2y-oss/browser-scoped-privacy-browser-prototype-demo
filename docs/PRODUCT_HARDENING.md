# Product hardening design

This document records the architectural decisions made after the 1.0.2 product
and security audit. The native WPF architecture remains appropriate: it keeps
presentation and orchestration in one process, removes the obsolete localhost
HTML UI, and leaves only Myst's existing loopback control API and the required
browser data-plane proxy.

## Identity and credential workflow

The controller now models all backend identities and requires an explicit
selection before any sensitive action when more than one identity exists. The
selected public address is the only identity state persisted by the app.

Create, import, unlock, register, and connect actions use a native `PasswordBox`
prompt. New identities require confirmation and a 12-character minimum.
Encrypted imports use Myst's `POST /identities-import` contract and preserve
the source passphrase as the new keystore passphrase. Unlock failure produces a
credential-specific, retryable message. Password controls and imported key
buffers are cleared after use; neither credentials nor private key material is
written to settings, activity, startup logs, or command-line arguments.

The loopback API necessarily receives a short-lived managed string because its
JSON contract requires one. Moving secrets into an OS credential vault would
increase persistence and does not remove that API-boundary conversion, so this
implementation asks for the passphrase per sensitive operation.

## Unified privacy readiness

The UI no longer derives “Ready” from `CONNECTED` alone. `BrowserLauncher`
owns a single readiness evaluator used by both presentation and launch. It
checks:

- backend connection state;
- app ownership and loopback-only binding of proxy port 4449;
- presence of the configured browser;
- presence and semantic contents of the locked policy;
- refusal to replace a different installed policy; and
- whether the isolated profile already has a tracked browser process.

Launch repeats the same gate to narrow time-of-check/time-of-use races. The
browser keeps a locked proxy with no direct fallback, so a backend failure or
controller shutdown leaves the browser offline rather than exposing traffic.

## Process and recovery model

The application admits one controller instance per Windows session, preventing
fixed-port and profile-lock collisions. The backend has explicit starting,
running, failed, crashed, and stopped states. A visible restart action performs
graceful shutdown first and kills only the owned process tree if necessary.

The launched browser process is tracked. Duplicate launches against the same
profile are refused, and browser exit updates the UI. Closing the controller
while the browser runs requires confirmation and explains the fail-closed
result.

`--skip-backend-launch` remains a diagnostic control-plane option. It cannot
launch the browser because a loopback listener alone is insufficient proof that
the adopted process is the intended traffic intermediary.

## Portable bundle identity

Packaging emits `bundle-manifest.json` for the controller executable, browser
executable, Myst backend, and policy. A packaged application verifies release
version, path containment, uniqueness, byte length, and SHA-256 before starting
Myst. Runtime overrides not covered by the manifest are rejected in a packaged
layout.

The manifest prevents accidental mixed or stale extraction. It is not a code
signature: archive authenticity still depends on the published SHA-256 and a
trusted download channel. Windows Authenticode signing remains release work.

## Remaining trust boundaries

- Myst's TequilAPI is fixed, unauthenticated loopback HTTP. A per-user named
  pipe or authenticated capability must be implemented upstream to remove this
  local-process trust exposure.
- Ports 44050 and 4449 remain fixed. Single-instance enforcement avoids
  collisions but does not provide future multi-profile concurrency.
- The persistent Mullvad Browser profile has no ephemeral/reset control yet.
- Identity export, removal, backup guidance, and OS-backed recovery remain.
- Provider, payment, registration availability, and provider behavior are
  external Mysterium dependencies.
- Windows standard-user, reboot, firewall-product, and signed-installer testing
  still require physical or virtual Windows validation.
