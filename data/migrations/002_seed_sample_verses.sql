-- ============================================================
-- Seed: 002_seed_sample_verses.sql
-- A handful of sample verses to bootstrap development
-- (Full dataset: load from quran-json or quran.com API)
-- ============================================================

INSERT INTO verses (surah_num, ayah_num, arabic_text, uthmani_text)
VALUES
  (1, 1, 'بِسْمِ اللَّهِ الرَّحْمَٰنِ الرَّحِيمِ',  'بِسۡمِ ٱللَّهِ ٱلرَّحۡمَٰنِ ٱلرَّحِيمِ'),
  (1, 2, 'الْحَمْدُ لِلَّهِ رَبِّ الْعَالَمِينَ',    'ٱلۡحَمۡدُ لِلَّهِ رَبِّ ٱلۡعَٰلَمِينَ'),
  (1, 3, 'الرَّحْمَٰنِ الرَّحِيمِ',                  'ٱلرَّحۡمَٰنِ ٱلرَّحِيمِ'),
  (1, 4, 'مَالِكِ يَوْمِ الدِّينِ',                  'مَٰلِكِ يَوۡمِ ٱلدِّينِ'),
  (1, 5, 'إِيَّاكَ نَعْبُدُ وَإِيَّاكَ نَسْتَعِينُ', 'إِيَّاكَ نَعۡبُدُ وَإِيَّاكَ نَسۡتَعِينُ'),
  (1, 6, 'اهْدِنَا الصِّرَاطَ الْمُسْتَقِيمَ',       'ٱهۡدِنَا ٱلصِّرَٰطَ ٱلۡمُسۡتَقِيمَ'),
  (1, 7, 'صِرَاطَ الَّذِينَ أَنْعَمْتَ عَلَيْهِمْ غَيْرِ الْمَغْضُوبِ عَلَيْهِمْ وَلَا الضَّالِّينَ',
          'صِرَٰطَ ٱلَّذِينَ أَنۡعَمۡتَ عَلَيۡهِمۡ غَيۡرِ ٱلۡمَغۡضُوبِ عَلَيۡهِمۡ وَلَا ٱلضَّآلِّينَ')
ON CONFLICT DO NOTHING;

INSERT INTO translations (verse_id, language, text, translator)
SELECT id, 'en',
  CASE ayah_num
    WHEN 1 THEN 'In the name of Allah, the Entirely Merciful, the Especially Merciful.'
    WHEN 2 THEN '[All] praise is [due] to Allah, Lord of the worlds -'
    WHEN 3 THEN 'The Entirely Merciful, the Especially Merciful,'
    WHEN 4 THEN 'Sovereign of the Day of Recompense.'
    WHEN 5 THEN 'It is You we worship and You we ask for help.'
    WHEN 6 THEN 'Guide us to the straight path -'
    WHEN 7 THEN 'The path of those upon whom You have bestowed favor, not of those who have earned [Your] anger or of those who are astray.'
  END,
  'Saheeh International'
FROM verses
WHERE surah_num = 1
ON CONFLICT DO NOTHING;
