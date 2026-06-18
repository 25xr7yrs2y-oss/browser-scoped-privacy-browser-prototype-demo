# Validation Results

## Current status

**Prototype partially works with known gaps.** Static configuration and
launcher safety tests pass. Real Mullvad Browser, a funded/registered Mysterium
identity, an active provider session, and packet-capture-backed S1-S7 execution
are not yet evidenced in this repository. Browser-only routing is therefore
not claimed.

| Scenario | Actual result | Evidence | Result |
|---|---|---|---|
| Configuration invariants | Locked loopback proxy, DoH/DNS prefetch off, WebRTC off, RFP and letterboxing on | `tests/Test-Configuration.ps1` | Pass |
| Launcher safety invariants | Listener ownership and lifecycle checks present; forbidden system network mutation absent | `tests/Test-Launcher.ps1` | Pass |
| S1 other apps unaffected | Not executed with real bundle | None | Inconclusive |
| S2 browser payload scoped | Not executed with active provider | None | Inconclusive |
| S3 DNS | Not executed with active provider | None | Inconclusive |
| S4 WebRTC | Policy locked off; packet proof not executed | Policy test only | Inconclusive |
| S5 fail closed | Architecture and policy implemented; packet proof not executed | Policy test only | Inconclusive |
| S6 cleanup/persistence | Launcher contains no system mutation; reboot comparison not executed | Launcher test only | Inconclusive |
| S7 non-admin | Code requires no elevation; real provider test not executed | Source inspection only | Inconclusive |

