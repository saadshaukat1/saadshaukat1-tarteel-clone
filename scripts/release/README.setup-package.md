# Tarteel — Windows Deployment Setup

This folder prepares a ready-to-ship Windows release of the Tarteel app:
a **portable, self-contained folder** that runs without installing the .NET
runtime or any other dependency. No MSIX/installer is produced — the app is a
framework-dependent WinUI desktop build (`WindowsPackageType=None`), so the
"setup" is an unzipped release folder with the exe plus all offline assets.

## What the script does

`make-setup.ps1`:

1. `dotnet publish` the app (Release, `win-x64`, self-contained) with the
   offline asset folders (`offline-assets/models`, `offline-assets/data`,
   `offline-assets/extras`) embedded as MAUI assets.
2. Copies the published output into `artifacts/setup/Tarteel/`.
3. Copies the offline assets into `Tarteel/offline-assets/` (models, data,
   extras) so the app's asset-extraction fallback has everything it needs.
4. Writes a `README.txt` into the release folder documenting system
   requirements and first run.
5. Zips everything into `artifacts/setup/Tarteel-<version>-win-x64.zip`.

Run from the repo root:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\release\make-setup.ps1
```

Optional version override:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\release\make-setup.ps1 -Version 1.1.0
```

## Output

- `artifacts/setup/Tarteel/` — the portable app folder.
- `artifacts/setup/Tarteel-<version>-win-x64.zip` — the distributable zip.

## Deploying

1. Copy the zip to the target machine (Windows 10 19041+ or Windows 11).
2. Extract anywhere (e.g. `C:\Tarteel`).
3. Run `Tarteel.exe`.

The app is self-contained: the .NET 9 runtime, WinUI/WinAppSDK bits, Whisper
native libs (`offline-assets/extras`), Whisper models
(`offline-assets/models`) and the full Quran dataset (`offline-assets/data`)
all ship inside the folder. User data (profiles, progress, streak, logs) is
written under `%LOCALAPPDATA%\TarteelClone` and `%LOCALAPPDATA%\Tarteel`.

## Notes

- Unsigned build — Windows SmartScreen may warn on first run; choose
  "More info → Run anyway". Signing can be added later via
  `windows-package.ps1 -CertPath ...`.
- `dotnet publish` uses the same `OfflineModelDir`/`OfflineDataDir`/
  `OfflineExtrasDir` MSBuild properties as the CI packaging script.
- The zip keeps relative paths (`Tarteel/...`) so extraction is clean.
