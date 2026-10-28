namespace TarteelClone.LocalRecitationCore.Models;

/// <summary>
/// The core pedagogical recitation modes in a traditional Quran madrasa / halaqah.
/// </summary>
public enum MadrasaRecitationMode
{
    /// <summary>
    /// السبق (Sabaq) - The daily new lesson to be memorized and recited to the Ustadh.
    /// High scrutiny: max 1-2 mistakes to advance.
    /// </summary>
    Sabaq = 0,

    /// <summary>
    /// السبقي / المعاضدة (Sabqi / Amokhta) - Revision of recently memorized lessons (last 5-10 pages).
    /// Prevents newly acquired memory from slipping.
    /// </summary>
    Sabqi = 1,

    /// <summary>
    /// المنزل / الدور (Manzil / Dhor) - Cumulative retention review across older Juz.
    /// Long-form recitation ensuring full Quran retention.
    /// </summary>
    Manzil = 2,

    /// <summary>
    /// اختبار الشيخ والمتشابهات (Ustadh Exam & Mutashabihat) - The teacher tests the student
    /// with random Ayah starts, similar verses (Mutashabihat), or surprise prompts.
    /// </summary>
    UstadhExam = 3
}

/// <summary>
/// Authentic Madrasa academic grades given by the Ustadh / Sheikh.
/// </summary>
public enum MadrasaGrade
{
    /// <summary>
    /// ممتاز (Mumtaz) - Flawless recitation with excellent tajweed (98–100%).
    /// </summary>
    Mumtaz = 0,

    /// <summary>
    /// جيد جداً (Jayyid Jiddan) - Very good, minor self-corrected slip (90–97%).
    /// </summary>
    JayyidJiddan = 1,

    /// <summary>
    /// جيد (Jayyid) - Good, acceptable retention with minor hesitation (80–89%).
    /// </summary>
    Jayyid = 2,

    /// <summary>
    /// مقبول (Maqbool) - Acceptable pass, requires immediate consolidation (70–79%).
    /// </summary>
    Maqbool = 3,

    /// <summary>
    /// إعادة (Iadah) - Must repeat the lesson tomorrow. Does not pass to new portion.
    /// </summary>
    Iadah = 4
}

/// <summary>
/// The Sheikh's pedagogical verdict on the student's recitation.
/// </summary>
public enum MadrasaTeacherDecision
{
    /// <summary>
    /// مجاز - ينتقل للدرس التالي (Passed - permitted to advance to next Sabaq).
    /// </summary>
    PassedToNext = 0,

    /// <summary>
    /// إعادة - يكرر الدرس غداً (Repeat - student must repeat this lesson tomorrow).
    /// </summary>
    RepeatLesson = 1,

    /// <summary>
    /// تثبيت ومراجعة - مراجعة مكثفة قبل الانتقال (Consolidate - review weak spots).
    /// </summary>
    ReviewWeakSpots = 2
}

/// <summary>
/// Official evaluation record issued by the Madrasa Ustadh AI.
/// </summary>
public sealed record MadrasaEvaluation(
    long Id,
    string UserKey,
    long? SessionId,
    int SurahNum,
    int AyahNum,
    int EndAyahNum,
    MadrasaRecitationMode Mode,
    MadrasaGrade Grade,
    double Score,
    int MistakesCount,
    int TajweedNotesCount,
    int LuqmahCount,
    int HesitationCount,
    MadrasaTeacherDecision Decision,
    string RemarksArabic,
    string RemarksEnglish,
    DateTimeOffset EvaluatedAt);

/// <summary>
/// Input for recording a Madrasa evaluation session.
/// </summary>
public sealed record MadrasaEvaluationInput(
    string UserKey,
    long? SessionId,
    int SurahNum,
    int AyahNum,
    int EndAyahNum,
    MadrasaRecitationMode Mode,
    int MistakesCount,
    int TajweedNotesCount,
    int LuqmahCount,
    int HesitationCount,
    double AccuracyPercentage);

/// <summary>
/// Challenge representing an oral test or Mutashabihat exam given by the Ustadh.
/// </summary>
public sealed record MutashabihaChallenge(
    int Id,
    string PromptVerseArabic,
    int SurahNum,
    int AyahNum,
    string SurahNameArabic,
    string SurahNameEnglish,
    string TargetContinuationArabic,
    string SimilarVerseSurahArabic,
    string ExplanationArabic,
    string ExplanationEnglish);
