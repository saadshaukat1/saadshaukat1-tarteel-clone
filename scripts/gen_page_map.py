import urllib.request, json, time
from concurrent.futures import ThreadPoolExecutor, as_completed

def fetch(page):
    url = f"https://api.quran.com/api/v4/verses/by_page/{page}?per_page=100"
    for attempt in range(4):
        try:
            req = urllib.request.Request(url, headers={"User-Agent": "curl/8"})
            with urllib.request.urlopen(req, timeout=30) as r:
                d = json.load(r)
            vs = d.get("verses", [])
            if not vs:
                return page, None
            first = vs[0]["verse_key"].split(":")
            last = vs[-1]["verse_key"].split(":")
            return page, {
                "page": page,
                "start_surah": int(first[0]), "start_ayah": int(first[1]),
                "end_surah": int(last[0]),   "end_ayah": int(last[1]),
            }
        except Exception as e:
            if attempt == 3:
                return page, f"ERR {e}"
            time.sleep(1.5)

results = {}
with ThreadPoolExecutor(max_workers=8) as ex:
    futs = [ex.submit(fetch, p) for p in range(1, 605)]
    for f in as_completed(futs):
        page, res = f.result()
        if isinstance(res, dict):
            results[page] = res
        else:
            print("FAIL", page, res)

pages = [results[p] for p in sorted(results)]
out = {"pages": pages}
with open("mobile/TarteelMobile/Resources/Raw/quran/mushaf/page_map.json", "w", encoding="utf-8") as f:
    json.dump(out, f, ensure_ascii=False, indent=2)

print("pages:", len(pages), "missing:", 604 - len(pages))
if pages:
    print("first:", pages[0], "last:", pages[-1])
