# Validation Results

## Current status

**Prototype partially works with known gaps.** Tests used Windows Server 2022,
Mullvad Browser 15.0.14, and the backend CI artifact for commit `227d63b`.
Fail-closed-at-launch and external-command scoping have packet evidence.
Provider-path validation is blocked because a newly created identity changed
from `InProgress` to `Unregistered`; quick-connect returned HTTP 500 with
`Identity ... is not registered`. Port 4449 never bound. Browser-only provider
routing is therefore not claimed.

| Scenario | Actual result | Evidence | Result |
|---|---|---|---|
| Configuration invariants | Locked loopback proxy, DoH/DNS prefetch off, WebRTC off, RFP and letterboxing on | `tests/Test-Configuration.ps1` | Pass |
| Launcher safety invariants | Listener ownership and lifecycle checks present; forbidden system network mutation absent | `tests/Test-Launcher.ps1` | Pass |
| S1 other apps unaffected | Exact PID trees for `curl` and `Invoke-WebRequest`: 80 socket records, 40 direct records to `162.159.140.220:443`, zero records involving 4449; direct IP `54.168.34.136`. An exploratory Edge run loaded a 564-byte page with no 4449 use, but its exact-PID rerun under SYSTEM did not render. | `evidence/windows-20260618/other-apps/` | Pass for curl/IWR; Edge evidence limited |
| S2 browser payload scoped | Backend node became healthy on 44050, but identity registration failed and 4449 never bound. | Backend HTTP error and logs summarized here | Inconclusive/blocker |
| S3 DNS | Browser policy disables native DNS paths, but no active provider existed for packet proof. | Policy test only | Inconclusive/blocker |
| S4 WebRTC | WebRTC is locked off; no active-provider packet test was possible. | Policy test only | Inconclusive/blocker |
| S5a backend absent at launch | 309 browser socket records: 96 `SynSent` attempts to `127.0.0.1:4449`, 78 internal loopback records, 135 bound sockets, and zero non-loopback browser sockets. | `evidence/windows-20260618/fail-closed/` | Pass |
| S5b backend crashes after launch | Requires an active provider/proxy first. | None | Inconclusive/blocker |
| S6 cleanup/persistence | Before/after user proxy unset; WinHTTP direct; DNS unchanged; default route unchanged. Both uninstallers exited 0; service/process/test paths absent. Reboot was not performed. | Results summarized here | Pass without reboot; reboot inconclusive |
| S7 non-admin | Source path is userspace, but live run used the Administrator account and the installer created an automatic supervisor service. | Source + service observation | Fail for installer; runtime non-admin inconclusive |

## Captured artifacts

- `fail-closed/capture.pcapng`: 1,280 bytes, SHA-256 recorded in the evidence README.
- `fail-closed/browser-sockets.csv`: time-aligned browser PID socket map.
- `other-apps/capture.pcapng`: 7,472,376 bytes.
- `other-apps/process-map.csv` and `sockets.csv`: exact root PID and descendant attribution.

## Host changes and cleanup

- Temporarily installed Mullvad Browser and the backend into
  `C:\Users\Administrator\Desktop\privacy-browser-live`.
- The backend installer created and started automatic service
  `MysteriumVPNSupervisor`; its uninstaller removed the service.
- Created a Mysterium identity in the pre-existing Administrator
  `.mysterium` state. It remains there because deleting shared prior state would
  be destructive.
- Restarted the pre-existing `sshd` service to recover repeated pre-key-exchange
  resets; it finished Running/Automatic.
- Stopped test browser, backend, Edge, curl, and PowerShell child processes.
- Removed the live bundle and remote project copy after collecting evidence.
