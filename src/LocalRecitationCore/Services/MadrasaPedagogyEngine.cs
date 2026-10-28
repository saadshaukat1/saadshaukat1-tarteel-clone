using TarteelClone.LocalRecitationCore.Models;

namespace TarteelClone.LocalRecitationCore.Services;

/// <summary>
/// Implements the authentic pedagogical evaluation logic of an experienced Madrasa Quran Ustadh / Sheikh.
/// </summary>
public static class MadrasaPedagogyEngine
{
    public static MadrasaEvaluation EvaluateRecitation(
        MadrasaEvaluationInput input,
        long evaluationId = 0,
        DateTimeOffset? evaluatedAt = null)
    {
        var timestamp = evaluatedAt ?? DateTimeOffset.UtcNow;
        var mode = input.Mode;
        var mistakes = input.MistakesCount;
        var tajweed = input.TajweedNotesCount;
        var luqmahs = input.LuqmahCount;
        var hesitations = input.HesitationCount;
        var accuracy = Math.Clamp(input.AccuracyPercentage, 0.0, 100.0);

        // Compute Grade according to traditional Madrasa rigor
        MadrasaGrade grade;
        MadrasaTeacherDecision decision;

        switch (mode)
        {
            case MadrasaRecitationMode.Sabaq:
                // Sabaq is the new lesson: strict criteria apply
                if (mistakes == 0 && luqmahs == 0 && tajweed <= 1 && accuracy >= 95.0)
                {
                    grade = MadrasaGrade.Mumtaz;
                    decision = MadrasaTeacherDecision.PassedToNext;
                }
                else if (mistakes <= 1 && luqmahs <= 1 && accuracy >= 88.0)
                {
                    grade = MadrasaGrade.JayyidJiddan;
                    decision = MadrasaTeacherDecision.PassedToNext;
                }
                else if (mistakes <= 2 && luqmahs <= 2 && accuracy >= 80.0)
                {
                    grade = MadrasaGrade.Jayyid;
                    decision = MadrasaTeacherDecision.PassedToNext;
                }
                else if (mistakes <= 3 && accuracy >= 70.0)
                {
                    grade = MadrasaGrade.Maqbool;
                    decision = MadrasaTeacherDecision.ReviewWeakSpots;
                }
                else
                {
                    grade = MadrasaGrade.Iadah;
                    decision = MadrasaTeacherDecision.RepeatLesson;
                }
                break;

            case MadrasaRecitationMode.Sabqi:
                // Sabqi (Amokhta) is recent revision (last 5-10 pages)
                if (mistakes == 0 && luqmahs <= 1 && accuracy >= 94.0)
                {
                    grade = MadrasaGrade.Mumtaz;
                    decision = MadrasaTeacherDecision.PassedToNext;
                }
                else if (mistakes <= 2 && luqmahs <= 2 && accuracy >= 85.0)
                {
                    grade = MadrasaGrade.JayyidJiddan;
                    decision = MadrasaTeacherDecision.PassedToNext;
                }
                else if (mistakes <= 3 && accuracy >= 75.0)
                {
                    grade = MadrasaGrade.Jayyid;
                    decision = MadrasaTeacherDecision.PassedToNext;
                }
                else if (accuracy >= 65.0)
                {
                    grade = MadrasaGrade.Maqbool;
                    decision = MadrasaTeacherDecision.ReviewWeakSpots;
                }
                else
                {
                    grade = MadrasaGrade.Iadah;
                    decision = MadrasaTeacherDecision.RepeatLesson;
                }
                break;

            case MadrasaRecitationMode.Manzil:
                // Manzil (Dhor) is long-form retention (0.5 to 1 full Juz)
                if (mistakes <= 1 && accuracy >= 92.0)
                {
                    grade = MadrasaGrade.Mumtaz;
                    decision = MadrasaTeacherDecision.PassedToNext;
                }
                else if (mistakes <= 3 && accuracy >= 85.0)
                {
                    grade = MadrasaGrade.JayyidJiddan;
                    decision = MadrasaTeacherDecision.PassedToNext;
                }
                else if (mistakes <= 5 && accuracy >= 75.0)
                {
                    grade = MadrasaGrade.Jayyid;
                    decision = MadrasaTeacherDecision.PassedToNext;
                }
                else if (accuracy >= 65.0)
                {
                    grade = MadrasaGrade.Maqbool;
                    decision = MadrasaTeacherDecision.ReviewWeakSpots;
                }
                else
                {
                    grade = MadrasaGrade.Iadah;
                    decision = MadrasaTeacherDecision.RepeatLesson;
                }
                break;

            case MadrasaRecitationMode.UstadhExam:
            default:
                // Oral Exam & Mutashabihat: zero tolerance for prompt dependency
                if (mistakes == 0 && luqmahs == 0 && hesitations <= 1 && accuracy >= 95.0)
                {
                    grade = MadrasaGrade.Mumtaz;
                    decision = MadrasaTeacherDecision.PassedToNext;
                }
                else if (mistakes <= 1 && luqmahs == 0 && accuracy >= 88.0)
                {
                    grade = MadrasaGrade.JayyidJiddan;
                    decision = MadrasaTeacherDecision.PassedToNext;
                }
                else if (mistakes <= 2 && accuracy >= 75.0)
                {
                    grade = MadrasaGrade.Jayyid;
                    decision = MadrasaTeacherDecision.ReviewWeakSpots;
                }
                else
                {
                    grade = MadrasaGrade.Iadah;
                    decision = MadrasaTeacherDecision.RepeatLesson;
                }
                break;
        }

        var (remarksAr, remarksEn) = GenerateUstadhRemarks(mode, grade, decision, mistakes, tajweed, luqmahs, hesitations);

        return new MadrasaEvaluation(
            evaluationId,
            input.UserKey,
            input.SessionId,
            input.SurahNum,
            input.AyahNum,
            input.EndAyahNum,
            mode,
            grade,
            accuracy,
            mistakes,
            tajweed,
            luqmahs,
            hesitations,
            decision,
            remarksAr,
            remarksEn,
            timestamp);
    }

    private static (string RemarksAr, string RemarksEn) GenerateUstadhRemarks(
        MadrasaRecitationMode mode,
        MadrasaGrade grade,
        MadrasaTeacherDecision decision,
        int mistakes,
        int tajweed,
        int luqmahs,
        int hesitations)
    {
        var modeNameAr = mode switch
        {
            MadrasaRecitationMode.Sabaq => "السبق",
            MadrasaRecitationMode.Sabqi => "السبقي (المعاضدة)",
            MadrasaRecitationMode.Manzil => "المنزل (الدور)",
            MadrasaRecitationMode.UstadhExam => "اختبار الشيخ والمتشابهات",
            _ => "التسميع"
        };

        var modeNameEn = mode switch
        {
            MadrasaRecitationMode.Sabaq => "Sabaq (New Lesson)",
            MadrasaRecitationMode.Sabqi => "Sabqi (Recent Revision)",
            MadrasaRecitationMode.Manzil => "Manzil (Cumulative Dhor)",
            MadrasaRecitationMode.UstadhExam => "Ustadh Oral Exam",
            _ => "Recitation"
        };

        return grade switch
        {
            MadrasaGrade.Mumtaz => (
                $"ما شاء الله، تبارك الرحمن! حفظ متقن وتلاوة خاشعة في {modeNameAr}. خالية من الأخطاء واللقمات. بارك الله فيك، مجاز للانتقال للدرس القادم.",
                $"Masha'Allah, flawless recitation in {modeNameEn}! Firm memorization without errors or prompts. Excellent tajweed. You are approved to advance."
            ),
            MadrasaGrade.JayyidJiddan => (
                $"أحسنت يا بني! أداء طيب جداً في {modeNameAr}. " +
                (luqmahs > 0 ? "احتاج التسميع إلى لقمة يسيرة، " : "") +
                (tajweed > 0 ? "مع الانتباه لأحكام التجويد (الغنة والقلقلة). " : "") +
                "الحفظ ممتاز ومجاز للانتقال مع التكرار.",
                $"Very good performance in {modeNameEn}. " +
                (luqmahs > 0 ? "Required one minor prompt. " : "") +
                (tajweed > 0 ? "Watch tajweed articulation (ghunnah/qalqalah). " : "") +
                "You are approved to advance with continued review."
            ),
            MadrasaGrade.Jayyid => (
                $"تسميع مقبول في {modeNameAr}. تم رصد ({mistakes}) من أخطاء الحفظ و({luqmahs}) لقمات. يُسمح لك بالانتقال بشرط مراجعة هذا الموضع ثلاث مرات مع زميلك بالحلقة.",
                $"Good retention in {modeNameEn}. Recorded {mistakes} mistake(s) and {luqmahs} prompt(s). Conditional pass: recite this portion 3 times before starting new verses."
            ),
            MadrasaGrade.Maqbool => (
                $"حفظك في {modeNameAr} يحتاج لتثبيت مكثف. كثرة التردد ({hesitations} مرات) والوقفات تدل على ضعف الحفظ. راجع المقطع كاملاً اليوم.",
                $"Passable recitation in {modeNameEn}, but retention is fragile. Frequent hesitation ({hesitations} pause(s)). Deeply review this section today."
            ),
            MadrasaGrade.Iadah or _ => (
                $"قرار الشيخ: إعادة {modeNameAr} غداً! رُصد ({mistakes}) أخطاء جليّة و({luqmahs}) لقمات. قاعدة الكتّاب: «لا ننتقل لآية حتى نتقن الحالية كالفاتحة». وفقك الله.",
                $"Ustadh Verdict: Repeat this {modeNameEn} tomorrow! Found {mistakes} clear mistake(s) and {luqmahs} prompt(s). Madrasa rule: 'Never advance until the current portion is firm as Al-Fatiha'."
            )
        };
    }

    public static string GetSpokenVerdictArabic(MadrasaGrade grade, MadrasaTeacherDecision decision)
    {
        return grade switch
        {
            MadrasaGrade.Mumtaz => "ما شاء الله، ممتاز! بارك الله فيك.",
            MadrasaGrade.JayyidJiddan => "أحسنت يا بني، حفظ طيب جداً.",
            MadrasaGrade.Jayyid => "جيد، لكن داوم على تكرار الآيات.",
            MadrasaGrade.Maqbool => "مقبول، راجع الأخطاء وثبّت حفظك.",
            MadrasaGrade.Iadah or _ => "أعد هذا السبق غداً، وركز جيداً."
        };
    }
}
