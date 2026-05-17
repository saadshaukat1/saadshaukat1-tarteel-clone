## Local Quran import assets

`seed_verses.json` is a practical bootstrap payload used to initialize the local SQLite store for offline desktop runs.

Future full import path:
- Add a complete dataset file as `full_quran.json` in this folder.
- Keep the same top-level JSON shape:
  - Either `{ "verses": [ ... ] }` or a plain array `[ ... ]`.
  - Each verse supports:
    - `surah_num`, `ayah_num`
    - `arabic_text`, optional `uthmani_text`
    - Optional `translations` array with `language`, `text`, optional `translator`
- On first app startup with an empty DB, the repository tries:
  1. `ExternalImportFile` path from configuration
  2. `quran/import/full_quran.json`
  3. `quran/import/seed_verses.json`
  4. Built-in fallback records
