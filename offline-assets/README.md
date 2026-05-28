# Offline Packaging Assets

This folder is the default packaging input root for Windows MAUI release artifacts.

## Expected structure

```text
offline-assets/
  models/        ? Whisper model binaries (auto-downloaded if missing)
  data/          ? Quran datasets / SQLite seed files
  extras/        ? whisper-cli.exe + supporting DLLs (must be present manually)
```

## What is already included

| File | Status |
|---|---|
| `extras/whisper-cli.exe` | ? Present — Whisper inference runtime |
| `extras/ggml-base.dll` | ? Present |
| `extras/ggml-cpu.dll` | ? Present |
| `extras/ggml.dll` | ? Present |
| `extras/whisper.dll` | ? Present |
| `data/quran/import/full_quran.json` | ? Present — full Quran import dataset |
| `models/ggml-small.bin` | ? Not included — **auto-downloaded on first launch** (~460 MB) |
| `models/ggml-medium.bin` | ? Not included — optional, manually place if needed |

## Model auto-download behaviour

When the app starts a recitation session for the first time:

1. `LocalWhisperAsrEngine.InitializeAsync` runs and calls `TryValidateRuntime`.
2. If `whisper-cli.exe` is found but the model file is **missing**, the engine immediately attempts to download the model from HuggingFace before falling back to mock mode.
3. Download URL (configured in `appsettings.json`):
   - **small**: `https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-small.bin`
4. The model is saved to `offline/models/ggml-small.bin` inside the app's deploy directory and cached for all future sessions.
5. If the download fails (no internet), the engine falls back to **mock mode** — audio is captured but no transcription is produced. The Whisper Debug Log panel in the app will show the exact reason.

> **To skip the download entirely:** Manually copy a `ggml-small.bin` (or `ggml-medium.bin`) into `offline-assets/models/` before packaging. The packaging script will bundle it with the app.

## Whisper Debug Log

While recording, the app shows a live dark debug panel:

| Icon | Meaning |
|---|---|
| ?? | Mock mode — `whisper-cli.exe` or model not found |
| ? | Whisper ran but returned an error (exit code / timeout) |
| ?? | Whisper ran but produced empty transcript (silence or low audio) |
| ? | Whisper produced a transcript — text shown |
| ?? | Verse matched — confidence score shown |

## Packaging

The packaging script (`scripts/release/windows-package.ps1`) forwards these directories to MSBuild as:

- `OfflineModelDir` ? `offline-assets/models`
- `OfflineDataDir` ? `offline-assets/data`
- `OfflineExtrasDir` ? `offline-assets/extras`

Output is a plain folder publish under `artifacts/windows-packaging/publish/`.  
No MSIX packaging — the app runs as a standard desktop executable.

You can override all paths through script parameters or a config JSON file:

```powershell
.\scripts\release\windows-package.ps1 `
  -ModelDir "C:\my-models" `
  -ExtrasDir "C:\my-extras"
```

