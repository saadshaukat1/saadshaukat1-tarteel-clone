# تارتيل کلون — نفاذی کی حیثیت (Implementation Status)

**Canonical guide:** `docs/implementation-status-english.md`
**ترجمہ شدہ status copy:** یہ فائل English guide کی verified حالت کا اردو ترجمہ ہے۔
**تاریخ:** 10 اگست 2026
**پروجیکٹ:** تارتيل کلون — قرآن حفظ اور تجوید کی درست کاری کے لیے مقامی ڈیسک �اپ ایپلی کیشن

---

## ۱. تعارف (Introduction)

یہ دستاویز تارتيل کلون پروجیکٹ کے موجودہ نفاذی کی حیثیت کو بیان کرتی ہے۔ مقصد ایک ایسا پیشہ ورانہ ایپلی کیشن بنانا ہے جو مکتب میں قرآن ٹیچر کی جگہ لے سکے — حفظ، تجوید کی غلطیوں کی نشاندہی، اور درست کرنے میں مدد دے۔

---

## ۲. جو مکمل ہو چکا ہے (What is Implemented)

### ۲.۱ آواز سے متن میں تبدیلی (ASR — Speech Recognition)
- ✅ Whisper.net کا مکمل انضمام (in-process C# bindings)
- ✅ ملٹی ٹئیر ماڈلز (tiny, base, small, medium) کی سپورٹ
- ✅ ماڈل خود بخود ڈاؤن لوڈ اور امپورٹ کی سہولت
- ✅ کراس چنک کنٹیکسٹ (cross-chunk context) — پچھلے 4 الفاظ کو یاد رکھنا
- ✅ سورہ کے نام سے Whisper کے ڈی کوڈنگ کو بائس کرنا (surah prompt)
- ✅ گریڈی (greedy) اور بیم سرچ (beam search) دونوں طریقے
- ✅ ورم اپ (warmup) میکانزم — پہلے استعمال پر ماڈل کو گرم کرنا

### ۲.۲ آیت میچنگ (Verse Matching)
- ✅ نیڈلمین-وونش (Needleman-Wunsch) الائنمنٹ الگورتھم
- ✅ لیوین شٹین (Levenshtein) + LCS فزی میچنگ
- ✅ پوزیشن اور کنفیدنس سکور کا حساب
- ✅ SQLite میں ورڈ انڈیکس کے ذریعے تیز تلاش
- ✅ سورہ کنٹیکسٹ کے مطابق امیدوار آیات کو محدود کرنا

### ۲.۳ آواز پائپ لائن (Audio Pipeline)
- ✅ پلیٹ فارم مخصوص مائک کیپچر (Windows, Android)
- ✅ 16kHz، 16-bit، mono PCM فارمیٹ
- ✅ خاموشی کا پتہ لگانا (silence detection)
- ✅ آر ایم ایس (RMS) نارملائزیشن
- ✅ 8 سیکنڈ چنکس + 1 سیکنڈ اوورلیپ

### ۲.۴ تجوید رول انجن (Tajweed Rule Engine)
- ✅ 7 قواعد کی نشاندہی: مد، غنہ، قلقله، ادغام، اخفاء، اقلا ب، اظهار
- ✅ **متن کی سطح پر** — Madd، Ghunna، Qalqalah، Idgham، Ikhfa، Iqlab، اور Izhar کی rule detection
- ✅ عربی rule tables اور feedback labels اب encoding-safe Unicode ہیں، اور سات direct regression tests سے verified ہیں
- ✅ **فونیم (phoneme) لیول کا تجزیہ** — `TajweedPhonemeAnalyzer` کے ذریعے:
  - ✅ **مد (Madd)** — آواز کی دیرانی (duration) کی پیمائش، 2–6 حرکات (500–3000ms)
  - ✅ **غنہ (Ghunna)** — FFT سپیکٹرل سینٹرائڈ کے ذریعے ناک کی گونج کا پتہ لگانا
  - ✅ **قلقله (Qalqalah)** — RMS برسٹ تناسب کے ذریعے plosive echo کی نشاندہی
- ✅ Word timestamps اب linear interpolation کی بجائے اصلی Whisper token timestamps (`SegmentData.Tokens`) سے بنائے جاتے ہیں
- ✅ Word alignment اب `SpokenToExpectedPosition` export کرتا ہے — ہر spoken word کا true ayah index
- ✅ Phoneme analyzer Windows WAV headers کو صحیح طریقے سے skip کرتا ہے
- ✅ Text engine اب تمام الفاظ کو madd/ghunna/nun-sakinah traits کے لیے check کرتا ہے، نہ صرف mismatched words
- ✅ Combined violations کو `(Position, Rule)` سے deduplicate کیا جاتا ہے، audio-evidence version کو ترجیح
- ✅ `OfflineRecitationOrchestrator` میں متن اور فونیم دونوں سطحوں کی غلطیوں کا مجموعہ، اور recitation UI میں feedback
- ❌ **مخارج (articulation points) کا تجزیہ نہیں** — `MakhrajError` اینوم موجود ہے مگر استعمال نہیں
- ❌ ادغام، اخفاء، اقلاب، اظہار کا phoneme-level تجزیہ ابھی مکمل نہیں

### ۲.۵ ڈیٹا بیس (Database)
- ✅ SQLite مکمل اسکیما: آیات، ترجمے، حفظ کی پیش رفت، جوز، سورتیں، ورڈ انڈیکس، مصحف صفحات
- ✅ EMA (Exponential Moving Average) کے ساتھ ماسٹری سکور
- ⚠️ **صرف سورہ الفاتحہ کا ڈیٹا موجود ہے** — باقی 6,236 آیات کا ڈیٹا نہیں
- ⚠️ مصحف صفحات کا نقشہ صرف 5 صفحات پر مشتمل ہے (604 کی ضرورت ہے)

### ۲.۶ 16 لائنر مصحف UI
- ✅ 16 قطاروں کا گرڈ اسٹرکچر
- ✅ 604 صفحات کی نیویگیشن
- ✅ آیت ہائی لائٹنگ
- ✅ سکرول کرنے پر خود بخود اگلے صفحے پر جانا
- ⚠️ **سچی 16 لائنر رینڈرنگ نہیں** — آیات کو قطاروں میں نہیں، بلکہ لگاتار رکھا گیا ہے
- ⚠️ انڈو پاک/فارسی رسم الخط کا متن موجود نہیں

### ۲.۷ پیش رفت ٹریکنگ (Progress Tracking)
- ✅ فی آیت ماسٹری سکور
- ✅ EMA ویٹڈ اپڈیٹ
- ✅ خلاصہ ویو (اوسط، ماسٹرڈ آیات)
- ❌ فی تجوید رول کی غلطیوں کا ریکارڈ نہیں

### ۲.۸ MAUI یوزر انٹرفیس
- ✅ تلاوت پیج (RecitationPage)
- ✅ مصحف پیج ویو (MushafPageView)
- ✅ پیش رفت پیج (ProgressPage)
- ✅ لاگ اِن پیج (LoginPage)
- ✅ ایڈوانسڈ ڈیبگ پینلز
- ✅ فری ری سائٹ موڈ (free-recite mode)

---

## ۳. خالی پروجیکٹس (Empty Projects)

یہ چار `src/` پروجیکٹس صرف اسکیفولڈ (scaffold) ہیں — ان میں صرف `bin/` اور `obj/` فولڈرز ہیں:

| پروجیکٹ | مقصد | حیثیت |
|---|---|---|
| **QuranEngine** | 16 لائنر ٹیکسٹ لے آؤٹ انجن، لائن لیول قرآنی ڈیٹا | خالی |
| **SearchService** | معنوی تلاش (AraBERT ایمبیڈنگز) | خالی |
| **UserService** | یوزر مینجمنٹ | خالی |
| **Api** | لوکل اے پی آئی لیئر | خالی |

---

## ۴. اہم خامیاں (Critical Gaps)

### ۴.۱ مکمل قرآن ڈیٹا
- ✅ `offline-assets/data/quran/import/full_quran.json` میں 6,236 آیات موجود ہیں۔
- ✅ `mobile/TarteelMobile/Resources/Raw/quran/mushaf/page_map.json` میں 604 صفحات موجود ہیں۔
- ✅ 114 سورتوں اور 30 پاروں کا حوالہ جاتی میٹا ڈیٹا موجود ہے۔
- ⚠️ انڈو پاک/فارسی رسم الخط کا مستند لائن لیول متن ابھی درکار ہے۔

### ۴.۲ فونیم لیول تجوید تجزیہ — مد، غنہ، قلقله ✅ مکمل، ابھی باقی: ادغام، اخفاء، مخارج
`TajweedPhonemeAnalyzer` کے ذریعے تین اہم قواعد کا فونیم سطح پر تجزیہ مکمل کر لیا گیا:
- ✅ **مد (Madd):** Whisper ورڈ ٹائمسٹیمپس کی بنیاد پر دورانیہ ناپتا ہے — مد کے حروف (ا، و، ي) پر 2–6 حرکات (500ms–3000ms) چیک کرتا ہے
- ✅ **غنہ (Ghunna):** FFT سپیکٹرل سینٹرائڈ تجزیہ — ن اور م کے الفاظ میں ناک کی گونج (nasal resonance) کا پتہ لگاتا ہے
- ✅ **قلقله (Qalqalah):** RMS برسٹ تجزیہ — plosive حروف (ق، ط، ب، ج، د) کے آخر میں echo/باؤنس کی تصدیق کرتا ہے
- ❌ **مخارج (Makharij):** ابھی تک لاگو نہیں — `MakhrajError` اینوم موجود ہے مگر استعمال نہیں
- ❌ **ادغام، اخفاء:** صرف متن کی سطح پر — فونیم تجزیہ نہیں
- ⚠️ درستگی Whisper کے ورڈ ٹائمسٹیمپس کے معیار پر منحصر ہے — بہتر ماڈلز (small, medium) بہتر نتائج دیتے ہیں

### ۴.۳ درست تلاوت کے لیے آواز پلے بیک نہیں
منصوبے میں "صحیح تلاوت کی پلے بیک" کا ذکر ہے — کوئی حوالہ آواز، پلے بیک میکانزم، یا فی الفظ صحیح تلاوت کے نمونے موجود نہیں۔

### ۴.۴ جائزہ شیڈول اور فی رول غلطیوں کا ریکارڈ
- ✅ `ReviewScheduler` کے ذریعے نئی آیت، کمزور کارکردگی، آج کے جائزے، اور تاخیر شدہ جائزے کی ترجیح بندی موجود ہے۔
- ✅ `memorization_progress` میں `next_review_at`، `attempt_count`، اور `recent_error_count` شامل ہیں، اور پرانے مقامی ڈیٹا بیس کے لیے additive upgrade موجود ہے۔
- ✅ Progress صفحے پر “Today’s review” کے ذریعے واجب الادا آیات الگ دکھائی جاتی ہیں۔
- ❌ فی تجوید رول کی مستقل تاریخ اور تفصیلی غلطی لاگ ابھی باقی ہے۔
- ✅ مستقل learning plans، lesson assignments، recitation sessions، verse-level attempts، mismatches، اور tajweed violations SQLite میں محفوظ ہیں۔
- ✅ Attempt لکھنے کا عمل transactional ہے، EMA progress اور next review کو `ReviewScheduler` کے ذریعے دوبارہ حساب کرتا ہے، اور نئی repository instance کے ذریعے restart recovery ثابت ہے۔
- ✅ Guided Today workflow اب learning plan کے مطابق محدود review/new-lesson assignments بناتا ہے، assignment-aware recitation شروع کرتا ہے، مکمل attempt محفوظ کرتا ہے، اور retry/next actions فراہم کرتا ہے۔
- ⚠️ Partial/error attempts ابھی صرف active session میں دکھائے جاتے ہیں؛ ان کی مستقل history اگلا مرحلہ ہے۔

### ۴.۵ سچی 16 لائنر رینڈرنگ نہیں
مصحف آیات کو لگاتار لیبلز میں رینڈر کرتا ہے — لائن لیول قرآنی متن کا استعمال نہیں کرتا جہاں ہر صفحے پر بالکل 16 قطاریں ہوں۔

---

## ۵. ترجیح بند ترتیب (Priority Order for Implementation)

1. **مستقل lesson assignments اور recitation history** — طالب علم کے لیے نیا سبق، حالیہ جائزہ، session، attempt، mismatch، اور تجوید غلطی کا مکمل ریکارڈ
2. **Partial/error attempt history** — ناکام یا جزوی recitation attempts کی مستقل history، completion states، اور بہتر retry guidance
3. **Full-mushaf verse matching** — `PlaceholderVerseMatcher` کو tested production matcher سے بدلنا، ayah continuation، assignment context، omissions اور repetitions سنبھالنا
4. **فی تجوید رول کی مستقل tracking** — بار بار آنے والی Madd، Ghunna، Idgham، Ikhfa، Iqlab، Izhar، اور Makhraj غلطیوں کی تاریخ اور feedback
5. **فونیم تجوید کی توسیع** — ادغام، اخفاء، اقلاب، اظہار، اور قابل اعتماد مخارج تجزیہ
6. **حوالہ آواز + پلے بیک** — لائسنس کے مطابق offline reciter audio اور verse/word playback
7. **سچی 16 لائنر رینڈرنگ** — verified Indo-Pak/Madani line-level text، فونٹ، اور ہر صفحے کی 16 مستند قطاریں
8. **طالب علم کا نصاب اور teacher-like guidance** — روزانہ ہدف، حفظ کا راستہ، streak، کمزور مقامات، اور اگلا واضح قدم

---

## ۶. نتیجہ (Conclusion)

تارتيل کلون کی بنیاد اب مکمل قرآن asset، local Whisper، آیت matching، SQLite progress، deterministic review scheduling، verified text-level tajweed feedback، اور durable lesson/attempt domain پر قائم ہے۔ Recitation screen کو demo کے لیے دوبارہ ترتیب دیا گیا ہے: verse workspace، fixed recording/status dock، typed mismatch rows، اور واضح tajweed coaching موجود ہیں۔ مکمل مکتب قاری متبادل بننے کے لیے guided Today workflow، full-mushaf continuation، reference playback، persistent per-rule history، اور teacher-like curriculum ابھی درکار ہیں۔

Guided Today workflow اب verified ہے: assignment-aware recitation، completion، retry، اور next-item recommendation موجود ہیں۔ اگلا عملی مرحلہ partial/error attempts کی مستقل history اور بہتر completion states ہیں۔

### ۷. تازہ verification evidence
- `dotnet build tests/TarteelMobile.Tests/TarteelMobile.Tests.csproj --framework net9.0-windows10.0.19041.0 --runtime win-x64 --no-restore` — 0 warnings، 0 errors
- `dotnet test tests/TarteelMobile.Tests/TarteelMobile.Tests.csproj --framework net9.0-windows10.0.19041.0 --runtime win-x64 --no-restore --filter FullyQualifiedName~TajweedAccuracyTests` — 14 passed، 0 failed
- `dotnet test tests/TarteelMobile.Tests/TarteelMobile.Tests.csproj --framework net9.0-windows10.0.19041.0 --runtime win-x64 --no-restore --filter FullyQualifiedName~TodayWorkflowServiceTests` — 1 passed، 0 failed
- `dotnet test tests/TarteelMobile.Tests/TarteelMobile.Tests.csproj --framework net9.0-windows10.0.19041.0 --runtime win-x64 --no-restore --filter FullyQualifiedName~LearningDomainRepositoryTests` — 2 passed، 0 failed
- `dotnet test tests/TarteelMobile.Tests/TarteelMobile.Tests.csproj --framework net9.0-windows10.0.19041.0 --runtime win-x64 --no-restore` — 34 passed، 0 failed
- `TajweedAccuracyTests` token timestamps، alignment، WAV handling، matched-word checks، phoneme analysis، اور dedup verify کرتا ہے۔
- Android `mobile/TarteelMobile/TarteelMobile.csproj` کے active `TargetFrameworks` میں شامل نہیں
- اگلا milestone ASR speed optimization (processor reuse، thread pinning، chunk tuning) ہے۔
