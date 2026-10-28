using TarteelClone.Api;
using TarteelClone.LocalRecitationCore.Models;
using TarteelClone.LocalRecitationCore.Services;
using TarteelMobile.Services;
using Xunit;

namespace TarteelMobile.Tests;

public sealed class MadrasaTeacherEngineTests
{
    [Fact]
    public void Sabaq_FlawlessRecitation_YieldsMumtazAndPassedToNext()
    {
        var input = new MadrasaEvaluationInput(
            UserKey: "student@madrasa.org",
            SessionId: 1,
            SurahNum: 67,
            AyahNum: 1,
            EndAyahNum: 5,
            Mode: MadrasaRecitationMode.Sabaq,
            MistakesCount: 0,
            TajweedNotesCount: 0,
            LuqmahCount: 0,
            HesitationCount: 0,
            AccuracyPercentage: 100.0);

        var eval = MadrasaPedagogyEngine.EvaluateRecitation(input);

        Assert.Equal(MadrasaGrade.Mumtaz, eval.Grade);
        Assert.Equal(MadrasaTeacherDecision.PassedToNext, eval.Decision);
        Assert.Contains("ما شاء الله", eval.RemarksArabic);
        Assert.Contains("Flawless", eval.RemarksEnglish, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Sabaq_SingleMinorMistake_YieldsJayyidJiddanAndPassed()
    {
        var input = new MadrasaEvaluationInput(
            UserKey: "student@madrasa.org",
            SessionId: 2,
            SurahNum: 67,
            AyahNum: 1,
            EndAyahNum: 5,
            Mode: MadrasaRecitationMode.Sabaq,
            MistakesCount: 1,
            TajweedNotesCount: 1,
            LuqmahCount: 1,
            HesitationCount: 1,
            AccuracyPercentage: 91.0);

        var eval = MadrasaPedagogyEngine.EvaluateRecitation(input);

        Assert.Equal(MadrasaGrade.JayyidJiddan, eval.Grade);
        Assert.Equal(MadrasaTeacherDecision.PassedToNext, eval.Decision);
        Assert.Contains("أحسنت", eval.RemarksArabic);
    }

    [Fact]
    public void Sabaq_MultipleMistakes_YieldsIadahAndRepeatLesson()
    {
        var input = new MadrasaEvaluationInput(
            UserKey: "student@madrasa.org",
            SessionId: 3,
            SurahNum: 67,
            AyahNum: 1,
            EndAyahNum: 5,
            Mode: MadrasaRecitationMode.Sabaq,
            MistakesCount: 4,
            TajweedNotesCount: 2,
            LuqmahCount: 3,
            HesitationCount: 4,
            AccuracyPercentage: 62.0);

        var eval = MadrasaPedagogyEngine.EvaluateRecitation(input);

        Assert.Equal(MadrasaGrade.Iadah, eval.Grade);
        Assert.Equal(MadrasaTeacherDecision.RepeatLesson, eval.Decision);
        Assert.Contains("إعادة", eval.RemarksArabic);
        Assert.Contains("Repeat", eval.RemarksEnglish, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Sabqi_ModerateTolerances_AllowsRetentionReview()
    {
        var input = new MadrasaEvaluationInput(
            UserKey: "student@madrasa.org",
            SessionId: 4,
            SurahNum: 67,
            AyahNum: 1,
            EndAyahNum: 10,
            Mode: MadrasaRecitationMode.Sabqi,
            MistakesCount: 2,
            TajweedNotesCount: 1,
            LuqmahCount: 1,
            HesitationCount: 2,
            AccuracyPercentage: 86.0);

        var eval = MadrasaPedagogyEngine.EvaluateRecitation(input);

        Assert.Equal(MadrasaGrade.JayyidJiddan, eval.Grade);
        Assert.Equal(MadrasaTeacherDecision.PassedToNext, eval.Decision);
    }

    [Fact]
    public void UstadhExam_ZeroToleranceForPrompts()
    {
        var input = new MadrasaEvaluationInput(
            UserKey: "student@madrasa.org",
            SessionId: 5,
            SurahNum: 2,
            AyahNum: 58,
            EndAyahNum: 58,
            Mode: MadrasaRecitationMode.UstadhExam,
            MistakesCount: 3,
            TajweedNotesCount: 1,
            LuqmahCount: 2,
            HesitationCount: 3,
            AccuracyPercentage: 70.0);

        var eval = MadrasaPedagogyEngine.EvaluateRecitation(input);

        Assert.Equal(MadrasaGrade.Iadah, eval.Grade);
        Assert.Equal(MadrasaTeacherDecision.RepeatLesson, eval.Decision);
    }

    [Fact]
    public void MutashabihatDatabase_ReturnsChallengesWithCorrectMetadata()
    {
        var challenges = MutashabihatDatabase.GetAllChallenges();
        Assert.NotEmpty(challenges);
        Assert.True(challenges.Count >= 5);

        var mulkChallenge = MutashabihatDatabase.GetChallengeForSurah(67);
        Assert.NotNull(mulkChallenge);
        Assert.Equal("Al-Mulk", mulkChallenge.SurahNameEnglish);
    }

    [Fact]
    public void LocalApiService_ReturnsMadrasaEvaluationsAndStatus()
    {
        var api = new LocalApiService();
        var status = api.GetStatus();

        Assert.True(status.IsReady);
        Assert.Contains("Madrasa", status.Message);

        var eval = api.EvaluateRecitation(new MadrasaEvaluationInput(
            "student", null, 67, 1, 2, MadrasaRecitationMode.Sabaq, 0, 0, 0, 0, 100.0));
        Assert.Equal(MadrasaGrade.Mumtaz, eval.Grade);

        var exam = api.GetRandomExamChallenge();
        Assert.NotNull(exam);
        Assert.NotEmpty(exam.PromptVerseArabic);
    }
}
