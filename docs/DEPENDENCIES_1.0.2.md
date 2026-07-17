# Version 1.0.2 bundled dependencies

The portable package is assembled without running either upstream installer.

## Mullvad Browser

- Version: 15.0.14, Windows x86-64
- Official release: <https://github.com/mullvad/mullvad-browser/releases/tag/15.0.14>
- Installer SHA-256: `56d5e332b1e780c6413c1a88e7b0a855ec1df5a400a26d92f08585637bc75c02`
- The browser application tree is extracted and placed under `vendor/mullvad-browser`.

## myst-lmprove node

- Commit: `227d63b052764595039c64beab9f3415cf01abdb`
- Pinned release asset: `MysteriumDark-Setup-0.0.0-snapshot.exe`
- Installer SHA-256: `8efe205063ea0fee05adb2d24012b4d3d843b6eacc4925a3cf3a3289625647da`
- Only the userspace `myst.exe` node is extracted into the portable package. The Electron shell, installer, and supervisor service are not included or executed.

## .NET runtime

The Windows x64 .NET 8 desktop runtime is included through self-contained publishing.
