# Offline Packaging Assets

This folder is the default packaging input root for Windows MAUI release artifacts.

## Expected structure

```text
offline-assets/
  models/
  data/
  extras/
```

## Purpose

- `models/`: local ASR model files (for example, Whisper model binaries).
- `data/`: offline Quran datasets or SQLite seed files.
- `extras/`: any additional runtime assets required offline.

## Default asset discovery

The desktop app auto-discovers Whisper runtime/model files when the recitation
pipeline first starts (not during app startup). If you place files in the default
locations below, no manual path edits are needed:

- Runtime executable:
  - `offline-assets/extras/whisper-cli.exe`
- Models:
  - `offline-assets/models/ggml-small.bin`
  - `offline-assets/models/ggml-medium.bin`

The app also checks packaged equivalents under `offline/extras` and `offline/models`
inside build output.

The packaging script (`scripts/release/windows-package.ps1`) forwards these directories to MSBuild as:

- `OfflineModelDir`
- `OfflineDataDir`
- `OfflineExtrasDir`

You can override all paths through script parameters or config JSON.
