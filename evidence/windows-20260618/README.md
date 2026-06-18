# Windows Evidence: 2026-06-18

Target: Windows Server 2022 (`54.168.34.136`, Tokyo). Raw packet captures are
paired with time-aligned Windows process/socket maps because pcapng does not
carry owning PID metadata.

## Fail closed with backend absent

- Browser: Mullvad Browser 15.0.14, locked proxy `127.0.0.1:4449`.
- 309 observations: 96 `SynSent` to 4449, 78 internal loopback records, 135
  bound sockets, zero non-loopback browser sockets.
- The headless screenshot was not produced, so visual error-page confirmation
  is missing. Socket evidence nevertheless shows repeated loopback proxy use
  and no direct fallback.
- `capture.pcapng` SHA-256: `531c685e27f1004e0ee6e627c7f51cc38babfc388380684434e0ec5d787cc55c`

## Other command-line apps unaffected

- Exact root PIDs and descendants were collected for `curl.exe` and the
  PowerShell process running `Invoke-WebRequest`.
- 80 observations: 40 bound and 40 direct established connections to
  `162.159.140.220:443`; zero use of port 4449.
- Direct public IP: `54.168.34.136`.
- Edge was launched in the same run under SYSTEM but did not render; an earlier
  Administrator exploratory run rendered 564 bytes without 4449 use. Treat the
  external-browser portion as weaker evidence than curl/IWR.
- `capture.pcapng` SHA-256: `0c0fd1a52fc6d646531110dacae4acf139fc677102744d542b90646402476f8f`

Large downloaded response bodies and ETL files were intentionally excluded.
