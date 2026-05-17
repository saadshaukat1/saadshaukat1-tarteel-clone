# 🕌 Tarteel Desktop (Offline-First)

Windows-first offline Quran recitation assistant built with .NET MAUI.
The app captures mic audio, performs on-device ASR, matches verses locally, and stores progress in local SQLite.

---

## 🏗️ Architecture (Current)

```
┌───────────────────────────────────────────────┐
│           .NET MAUI Desktop App               │
│  Recitation UI · Progress UI · Local Session  │
└───────────────────┬───────────────────────────┘
                    │ in-process calls
┌───────────────────▼───────────────────────────┐
│              Local Recitation Core            │
│  Audio chunk orchestration · Matching hooks   │
└───────────────┬───────────────────────────────┘
                │
┌───────────────▼──────────────┐  ┌──────────────────────────┐
│ Local Whisper Adapter        │  │ Local Verse Repository   │
│ Runtime + model auto-discover│  │ SQLite + import fallback │
└──────────────────────────────┘  └──────────────────────────┘
```

---

## 📁 Repository Structure

```
tarteel-clone/
├── TarteelClone.slnx
├── mobile/
│   └── TarteelMobile/                 # Windows desktop MAUI app
├── src/
│   └── LocalRecitationCore/           # Shared local recitation pipeline contracts/services
├── offline-assets/
│   ├── models/                        # local ASR models
│   ├── data/                          # optional offline Quran dataset files
│   └── extras/                        # whisper runtime executable and helpers
├── scripts/release/                   # Windows packaging automation
└── README.md
```

---

## 🚀 Quick Start (Offline Desktop)

### Prerequisites
- .NET 10 SDK
- MAUI workload for Windows
- Windows 10/11

### 1) Place offline runtime assets (recommended defaults)

- `offline-assets/extras/whisper-cli.exe`
- `offline-assets/models/ggml-small.bin`
- optional: `offline-assets/models/ggml-medium.bin`

### 2) Build desktop app

```powershell
dotnet restore mobile/TarteelMobile/TarteelMobile.csproj `
  -p:TargetFramework=net10.0-windows10.0.19041.0 `
  -p:RuntimeIdentifier=win-x64 `
  -p:MSBuildEnableWorkloadResolver=false

dotnet build mobile/TarteelMobile/TarteelMobile.csproj `
  -f net10.0-windows10.0.19041.0 `
  -p:RuntimeIdentifier=win-x64 `
  -p:MSBuildEnableWorkloadResolver=false
```

### 3) Run desktop app

```powershell
dotnet run --project mobile/TarteelMobile/TarteelMobile.csproj `
  -f net10.0-windows10.0.19041.0 `
  -p:RuntimeIdentifier=win-x64 `
  -p:MSBuildEnableWorkloadResolver=false
```

---

## 🪟 Windows Offline Packaging

Initial Windows packaging/release automation for MAUI now exists under `scripts/release/`.

- Script: `scripts/release/windows-package.ps1`
- Config template: `scripts/release/windows-packaging.config.example.json`
- Detailed guide: `scripts/release/README.windows-packaging.md`
- CI workflow: `.github/workflows/windows-packaging-release.yml`

### Build-only validation (no signing, local/offline friendly)

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\release\windows-package.ps1 -BuildOnly
```

### Publish + MSIX packaging (unsigned)

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\release\windows-package.ps1
```

### Configure packaged offline assets

The script injects asset roots into MAUI packaging via:

- `OfflineModelDir` (default `offline-assets/models`)
- `OfflineDataDir` (default `offline-assets/data`)
- `OfflineExtrasDir` (default `offline-assets/extras`)

If these folders are absent, packaging continues and logs warnings.

### Signing-ready (credentials optional)

Unsigned output works by default (`AppxPackageSigningEnabled=false`). When certificates are available, enable signing using:

- `-CertPath` and `WINDOWS_CERT_PASSWORD` (or `-CertPassword`)
- or `-CertThumbprint`

This keeps the release flow repeatable without requiring secrets during setup.

---

## 📜 License

MIT — see [LICENSE](LICENSE).
