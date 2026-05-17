# Windows Packaging and Release (MAUI)

This folder provides an initial, repeatable Windows packaging scaffold for `TarteelMobile` that supports local/offline release packaging and optional signing.

## Files

- `windows-package.ps1`: end-to-end packaging script (restore + build/publish).
- `windows-packaging.config.example.json`: example config for local/offline packaging.

## What the script does

- Restores and builds/publishes `mobile/TarteelMobile/TarteelMobile.csproj` for Windows.
- Passes configurable offline asset directories into MSBuild:
  - `OfflineModelDir`
  - `OfflineDataDir`
  - `OfflineExtrasDir`
- Supports two modes:
  - Build-only validation (`-BuildOnly`) to verify packaging inputs without emitting MSIX.
  - Publish + MSIX packaging (default) to produce release artifacts.
- Supports signing-ready options but does not require credentials:
  - Unsinged package flow works with no cert inputs.
  - Signed package flow can be enabled by setting certificate path/thumbprint and password.

## Quick start (local, unsigned)

From repo root:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\release\windows-package.ps1 -BuildOnly
```

Run with config file:

```powershell
Copy-Item .\scripts\release\windows-packaging.config.example.json .\scripts\release\windows-packaging.config.json
powershell -ExecutionPolicy Bypass -File .\scripts\release\windows-package.ps1 -ConfigPath .\scripts\release\windows-packaging.config.json -BuildOnly
```

## Dry run

Print all resolved commands and inputs without running `dotnet`:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\release\windows-package.ps1 -DryRun
```

## Publish MSIX (unsigned)

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\release\windows-package.ps1
```

Artifacts are emitted under `artifacts/windows-packaging`.

## Enable signing later

The script is signing-ready and supports either:

- `-CertPath <path-to-pfx>` (+ `-CertPassword` or `WINDOWS_CERT_PASSWORD`)
- `-CertThumbprint <thumbprint>`

Example:

```powershell
$env:WINDOWS_CERT_PASSWORD="your-secret"
powershell -ExecutionPolicy Bypass -File .\scripts\release\windows-package.ps1 -CertPath .\certs\release.pfx
```

If no cert options are provided, script falls back to unsigned packaging (`AppxPackageSigningEnabled=false`).
