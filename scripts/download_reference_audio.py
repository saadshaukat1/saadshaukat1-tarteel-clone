#!/usr/bin/env python3
"""
Tarteel Clone — Reference Recitation Audio Downloader & Curator

Downloads and organizes offline verse-by-verse recitation audio for Tarteel Clone.
Audio files are stored under:
    <destination>/reference_audio/{surah:000}/{ayah:000}.mp3 (or .wav)

Recommended Reciters (Public Domain / Creative Commons / Educational Use):
- Husary (Murattal / Educational Tajweed Pacing):
  URL prefix: https://everyayah.com/data/Husary_128kbps/
- Alafasy (Clear Modern Murattal):
  URL prefix: https://everyayah.com/data/Alafasy_128kbps/
- Abdul_Basit_Murattal:
  URL prefix: https://everyayah.com/data/Abdul_Basit_Murattal_192kbps/

Usage:
    python download_reference_audio.py --surah 1 --reciter husary --out-dir ./offline-assets/audio
    python download_reference_audio.py --juz 30 --reciter alafasy --out-dir ./offline-assets/audio
    python download_reference_audio.py --all --reciter husary --out-dir ./offline-assets/audio
"""

import argparse
import os
import sys
import time
import urllib.request
import urllib.error

RECITERS = {
    "husary": {
        "name": "Sheikh Mahmoud Khalil Al-Husary (Murattal)",
        "url_base": "https://everyayah.com/data/Husary_128kbps",
        "license": "Public Domain / Educational Quranic Use",
        "description": "Standard pedagogical baseline for classical tajweed rules."
    },
    "alafasy": {
        "name": "Sheikh Mishary Rashid Alafasy",
        "url_base": "https://everyayah.com/data/Alafasy_128kbps",
        "license": "Creative Commons / Open Quranic Dataset",
        "description": "High fidelity, clear contemporary murattal recitation."
    },
    "abdulbasit": {
        "name": "Sheikh Abdulbasit Abdulsamad (Murattal)",
        "url_base": "https://everyayah.com/data/Abdul_Basit_Murattal_192kbps",
        "license": "Public Domain / Classical Broadcast Archive",
        "description": "Renowned Egyptian classical recitation master."
    }
}

# Total verses per surah (1 to 114)
SURAH_AYAH_COUNTS = [
    7, 286, 200, 176, 120, 165, 206, 75, 129, 109,
    123, 111, 43, 52, 99, 128, 111, 110, 98, 135,
    112, 78, 118, 64, 77, 227, 93, 88, 69, 60,
    34, 30, 73, 54, 45, 83, 182, 88, 75, 85,
    54, 53, 89, 59, 37, 35, 38, 29, 18, 45,
    60, 49, 62, 55, 78, 96, 29, 22, 24, 13,
    14, 11, 11, 18, 12, 12, 30, 52, 52, 44,
    28, 28, 20, 56, 40, 31, 50, 40, 46, 42,
    29, 19, 36, 25, 22, 17, 19, 26, 30, 20,
    15, 21, 11, 8, 8, 19, 5, 8, 8, 11,
    11, 8, 3, 9, 5, 4, 7, 3, 6, 3,
    5, 4, 5, 6
]

# Surahs in Juz 30 (Surahs 78 to 114)
JUZ_30_SURAHS = list(range(78, 115))


def download_verse(url_base: str, surah: int, ayah: int, target_dir: str, retries: int = 3) -> bool:
    """Downloads a single verse audio file and saves as {ayah:000}.mp3"""
    filename = f"{surah:03d}{ayah:03d}.mp3"
    url = f"{url_base}/{filename}"
    out_path = os.path.join(target_dir, f"{ayah:03d}.mp3")

    if os.path.exists(out_path) and os.path.getsize(out_path) > 1024:
        return True  # Already cached

    for attempt in range(1, retries + 1):
        try:
            req = urllib.request.Request(url, headers={"User-Agent": "TarteelClone-AudioCurator/1.0"})
            with urllib.request.urlopen(req, timeout=15) as resp:
                data = resp.read()
                with open(out_path, "wb") as f:
                    f.write(data)
            return True
        except (urllib.error.URLError, TimeoutError, ConnectionError) as e:
            if attempt == retries:
                print(f"  [ERROR] Failed to download {url}: {e}", file=sys.stderr)
                return False
            time.sleep(1.0 * attempt)
    return False


def main():
    parser = argparse.ArgumentParser(description="Tarteel Clone Reference Audio Curator")
    parser.add_argument("--reciter", choices=list(RECITERS.keys()), default="husary",
                        help="Reciter identifier (default: husary)")
    parser.add_argument("--surah", type=int, choices=range(1, 115),
                        help="Specific surah number (1-114)")
    parser.add_argument("--juz", type=int, choices=range(1, 31),
                        help="Specific juz number (e.g. 30)")
    parser.add_argument("--all", action="store_true",
                        help="Download all 114 surahs (6,236 verses)")
    parser.add_argument("--out-dir", default="./offline-assets/audio",
                        help="Target output root directory")

    args = parser.parse_args()

    reciter_info = RECITERS[args.reciter]
    print("=" * 60)
    print(f"Tarteel Clone — Offline Audio Curation")
    print(f"Reciter: {reciter_info['name']}")
    print(f"License: {reciter_info['license']}")
    print(f"Description: {reciter_info['description']}")
    print("=" * 60)

    target_surahs = []
    if args.surah:
        target_surahs = [args.surah]
    elif args.juz == 30:
        target_surahs = JUZ_30_SURAHS
    elif args.all:
        target_surahs = list(range(1, 115))
    else:
        # Default to Juz 30 if nothing specified
        print("No scope specified. Defaulting to Juz 30 (Surahs 78–114)...")
        target_surahs = JUZ_30_SURAHS

    base_out = os.path.join(args.out_dir, "reference_audio")
    os.makedirs(base_out, exist_ok=True)

    total_downloaded = 0
    total_failed = 0

    for surah_num in target_surahs:
        surah_dir = os.path.join(base_out, f"{surah_num:03d}")
        os.makedirs(surah_dir, exist_ok=True)

        ayah_count = SURAH_AYAH_COUNTS[surah_num - 1]
        print(f"Downloading Surah {surah_num:03d} ({ayah_count} verses)...", end="", flush=True)

        surah_ok = 0
        for ayah_num in range(1, ayah_count + 1):
            ok = download_verse(reciter_info["url_base"], surah_num, ayah_num, surah_dir)
            if ok:
                surah_ok += 1
                total_downloaded += 1
            else:
                total_failed += 1

        print(f" Done ({surah_ok}/{ayah_count})")

    print("=" * 60)
    print(f"Download complete: {total_downloaded} verses ready, {total_failed} failed.")
    print(f"Destination: {os.path.abspath(base_out)}")
    print("=" * 60)


if __name__ == "__main__":
    main()
