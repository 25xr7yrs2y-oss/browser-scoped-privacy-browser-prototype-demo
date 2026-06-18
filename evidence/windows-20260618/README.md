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

## Active provider payload

- Direct host IP: `54.168.34.136`; proxy exit IP: `64.110.102.90`.
- `myst.exe` owned the listener at `127.0.0.1:4449`.
- Browser: 433 TCP observations, including 173 established connections to
  4449; zero non-loopback TCP, TCP DNS/DoT, or UDP endpoint observations.
- Backend: 327 separately attributed TCP observations, including direct
  control/provider-plane traffic.
- `provider-payload/capture.pcapng` SHA-256:
  `62d2936b6ba67bac9d0d9015e7fa0c289ccbe2c8b863ccf8e94dac687fcd1037`

## Backend crash

- The process owning 4449 was terminated and listener absence was verified.
- A fresh browser recorded 228 TCP observations: 78 `SynSent` attempts to the
  dead loopback proxy, 48 internal loopback records, 102 bound records, and
  zero direct destination records.
- `backend-crash/capture.pcapng` SHA-256:
  `0fd9789f647afbc128745d85dca87de61a43d2422ab1601619f036e3fa4980c6`
