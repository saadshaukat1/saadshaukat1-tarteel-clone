Full Quran dataset options
==========================

1. quran-json (MIT license)
   https://github.com/risan/quran-json
   Format: JSON per surah, includes Arabic text + translations
   Usage: run `data/scripts/import_quran_json.py` after downloading

2. Tanzil.net downloads
   https://tanzil.net/download/
   Format: Plain text / XML, Uthmani script
   License: CC BY 3.0

3. Al-Quran Cloud API
   https://alquran.cloud/api
   Endpoint: GET https://api.alquran.cloud/v1/quran/quran-uthmani
   Returns all 6236 verses in a single request

Place raw data files in this directory before running migrations.
