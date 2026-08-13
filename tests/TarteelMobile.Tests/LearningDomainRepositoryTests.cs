using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using TarteelClone.LocalRecitationCore.Models;
using TarteelMobile.Services;
using Xunit;

namespace TarteelMobile.Tests;

public sealed class LearningDomainRepositoryTests
{
    [Fact]
    public async Task LearningPlanAssignmentsAndSessionLifecycle_PersistAcrossRepositoryRestart()
    {
        var databaseName = $"learning-domain-{Guid.NewGuid():N}.db";
        try
        {
            var first = CreateRepository(databaseName);
            var plan = await first.GetOrCreateLearningPlanAsync("student@example.com", new LearningPlanInput(2, 6));
            var assignments = await first.CreateAssignmentsAsync(
                "student@example.com",
                [new LessonAssignmentInput(1, 1, LessonAssignmentReason.NewLesson)]);
            var session = await first.OpenRecitationSessionAsync("student@example.com");

            Assert.Equal(2, plan.DailyNewLessonTarget);
            Assert.Single(assignments);
            Assert.Equal(RecitationSessionStatus.Open, session.Status);

            var closed = await first.CloseRecitationSessionAsync(session.Id);
            Assert.Equal(RecitationSessionStatus.Completed, closed.Status);

            var second = CreateRepository(databaseName);
            var recoveredPlan = await second.GetOrCreateLearningPlanAsync("student@example.com");
            var recoveredAssignments = await second.GetAssignmentsAsync("student@example.com");
            var recoveredSessions = await second.GetRecitationSessionsAsync("student@example.com");

            Assert.Equal(plan.Id, recoveredPlan.Id);
            Assert.Single(recoveredAssignments);
            Assert.Equal(assignments[0].Id, recoveredAssignments[0].Id);
            Assert.Single(recoveredSessions);
            Assert.Equal(RecitationSessionStatus.Completed, recoveredSessions[0].Status);
        }
        finally
        {
            DeleteDatabase(databaseName);
        }
    }

    [Fact]
    public async Task VerseAttempt_PersistsOneAttemptWithMismatchAndTajweedErrors_AndRecomputesProgress()
    {
        var databaseName = $"learning-attempt-{Guid.NewGuid():N}.db";
        try
        {
            var repository = CreateRepository(databaseName);
            var assignment = Assert.Single(await repository.CreateAssignmentsAsync(
                "student@example.com",
                [new LessonAssignmentInput(1, 1, LessonAssignmentReason.Review)]));
            var session = await repository.OpenRecitationSessionAsync("student@example.com");
            var attemptedAt = new DateTimeOffset(2026, 8, 10, 12, 0, 0, TimeSpan.Zero);
            var input = new VerseAttemptInput(
                session.Id,
                assignment.Id,
                1,
                1,
                0.82,
                0.91,
                "spoken text",
                [new RecitationWordMismatch(2, "spoken", "expected")],
                [new TajweedViolation(2, TajweedRuleType.Ghunna, "expected", "spoken", "Keep the nasal sound")],
                attemptedAt);

            var saved = await repository.SaveVerseAttemptAsync("student@example.com", input);
            var attempts = await repository.GetVerseAttemptsAsync("student@example.com");
            var progress = Assert.Single(await repository.GetProgressAsync("student@example.com"));
            var recoveredAssignment = Assert.Single(await repository.GetAssignmentsAsync("student@example.com"));

            Assert.Equal(saved.Id, attempts[0].Id);
            Assert.Single(attempts[0].Mismatches);
            Assert.Equal("expected", attempts[0].Mismatches[0].Expected);
            Assert.Single(attempts[0].TajweedViolations);
            Assert.Equal(TajweedRuleType.Ghunna, attempts[0].TajweedViolations[0].Rule);
            Assert.Equal(1, progress.AttemptCount);
            Assert.Equal(0.82, progress.MasteryScore, 3);
            Assert.Equal(attemptedAt.AddHours(12), progress.NextReviewAt);
            Assert.Equal(LessonAssignmentStatus.Completed, recoveredAssignment.Status);

            var second = CreateRepository(databaseName);
            var recoveredAttempts = await second.GetVerseAttemptsAsync("student@example.com", session.Id);
            Assert.Single(recoveredAttempts);
            Assert.Single(recoveredAttempts[0].Mismatches);
            Assert.Single(recoveredAttempts[0].TajweedViolations);
        }
        finally
        {
            DeleteDatabase(databaseName);
        }
    }

    [Fact]
    public async Task Streak_CountsConsecutiveDaysAndBreaksOnGap()
    {
        var databaseName = $"learning-streak-{Guid.NewGuid():N}.db";
        try
        {
            var repository = CreateRepository(databaseName);
            var userKey = "streak@example.com";
            var today = new DateTimeOffset(2026, 8, 11, 10, 0, 0, TimeSpan.Zero);

            // Days 8/9 and 8/11 — gap on 8/10 breaks the run.
            await repository.RecordPracticeDayAsync(userKey, new DateTimeOffset(2026, 8, 9, 9, 0, 0, TimeSpan.Zero));
            await repository.RecordPracticeDayAsync(userKey, new DateTimeOffset(2026, 8, 11, 9, 0, 0, TimeSpan.Zero));

            var streak = await repository.GetStreakAsync(userKey, today);
            Assert.Equal(1, streak.Current);   // only today (8/11)
            Assert.Equal(1, streak.Best);      // longest run is a single day
            Assert.Equal(2, streak.TotalDays);

            // Fill the gap → current streak becomes 3 (8/9, 8/10, 8/11).
            await repository.RecordPracticeDayAsync(userKey, new DateTimeOffset(2026, 8, 10, 9, 0, 0, TimeSpan.Zero));
            var grown = await repository.GetStreakAsync(userKey, today);
            Assert.Equal(3, grown.Current);
            Assert.Equal(3, grown.Best);
            Assert.Equal(3, grown.TotalDays);
        }
        finally
        {
            DeleteDatabase(databaseName);
        }
    }

    [Fact]
    public async Task CurriculumPosition_PersistsAcrossRepositoryRestart()
    {
        var databaseName = $"learning-curriculum-{Guid.NewGuid():N}.db";
        try
        {
            var repository = CreateRepository(databaseName);
            await repository.SetCurriculumPositionAsync(TarteelMobile.Services.CurriculumPath.Juz30, 42, "student@example.com");

            var second = CreateRepository(databaseName);
            var (path, position) = await second.GetCurriculumPositionAsync("student@example.com");
            Assert.Equal(TarteelMobile.Services.CurriculumPath.Juz30, path);
            Assert.Equal(42, position);
        }
        finally
        {
            DeleteDatabase(databaseName);
        }
    }

    [Fact]
    public async Task WeakVerseRecommendations_OrderByErrorCountDescending()
    {
        var databaseName = $"learning-weak-{Guid.NewGuid():N}.db";
        try
        {
            var repository = CreateRepository(databaseName);
            var userKey = "weak@example.com";
            var session = await repository.OpenRecitationSessionAsync(userKey);
            var attemptedAt = new DateTimeOffset(2026, 8, 11, 12, 0, 0, TimeSpan.Zero);

            await repository.SaveVerseAttemptAsync(userKey, new VerseAttemptInput(
                session.Id, null, 2, 255, 0.55, 0.6, "text",
                [new RecitationWordMismatch(0, "a", "b")],
                [new TajweedViolation(0, TajweedRuleType.Madd, "a", "b", "hint"),
                 new TajweedViolation(1, TajweedRuleType.Ghunna, "a", "b", "hint")],
                attemptedAt));
            await repository.SaveVerseAttemptAsync(userKey, new VerseAttemptInput(
                session.Id, null, 1, 1, 0.6, 0.7, "text",
                [new RecitationWordMismatch(0, "a", "b")],
                [new TajweedViolation(0, TajweedRuleType.Madd, "a", "b", "hint")],
                attemptedAt.AddMinutes(1)));

            var weak = await repository.GetWeakVerseRecommendationsAsync(userKey);
            Assert.Equal(2, weak.Count);
            Assert.Equal(2, weak[0].SurahNum);   // 2 errors first
            Assert.Equal(255, weak[0].AyahNum);
            Assert.Equal(1, weak[1].SurahNum);   // 1 error second
            Assert.Equal(2, weak[0].ErrorCount);
        }
        finally
        {
            DeleteDatabase(databaseName);
        }
    }

    private static LocalVerseRepository CreateRepository(string databaseName) =>
        new(
            Options.Create(new LocalQuranDataOptions
            {
                DatabaseFileName = databaseName,
                PreferredAssetImportFile = string.Empty,
                FallbackSeedAssetImportFile = string.Empty,
                DefaultUserKey = "offline-test"
            }),
            new TestDiagnosticsService());

    private static void DeleteDatabase(string databaseName)
    {
        SqliteConnection.ClearAllPools();
        var path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TarteelClone",
            "data",
            databaseName);
        foreach (var suffix in new[] { "", "-wal", "-shm" })
        {
            var candidate = path + suffix;
            if (File.Exists(candidate))
            {
                File.Delete(candidate);
            }
        }
    }

    private sealed class TestDiagnosticsService : IAppDiagnosticsService
    {
        public string LogPath => string.Empty;
        public void Info(string message) { }
        public void Warn(string message) { }
        public void Error(string message, Exception? exception = null) { }
        public Task<IReadOnlyList<string>> ReadRecentAsync(int maxLines = 200) => Task.FromResult<IReadOnlyList<string>>([]);
    }
}
