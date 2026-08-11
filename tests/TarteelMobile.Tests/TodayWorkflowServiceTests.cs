using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using TarteelClone.LocalRecitationCore.Models;
using TarteelMobile.Models;
using TarteelMobile.Services;
using Xunit;

namespace TarteelMobile.Tests;

public sealed class TodayWorkflowServiceTests
{
    [Fact]
    public async Task TodayQueue_UsesReviewBeforeNewLessonAndDoesNotDuplicate()
    {
        var repository = new FakeRepository
        {
            Plan = new LearningPlan(1, "student", 1, 1, DateTimeOffset.UtcNow, true),
            Progress = [new VerseProgress(1, 1, "review", 0.3, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(-1), 2, 1)],
            Verses =
            [
                new Verse { SurahNum = 1, AyahNum = 1, ArabicText = "review" },
                new Verse { SurahNum = 1, AyahNum = 2, ArabicText = "new" }
            ]
        };
        var service = new TodayWorkflowService(repository);

        var first = await service.GetTodayAssignmentsAsync("student", DateTimeOffset.UtcNow);
        var second = await service.GetTodayAssignmentsAsync("student", DateTimeOffset.UtcNow);

        Assert.Equal(2, first.Count);
        Assert.Equal(LessonAssignmentReason.Review, first[0].Assignment.Reason);
        Assert.Equal(LessonAssignmentReason.NewLesson, first[1].Assignment.Reason);
        Assert.Equal(2, second.Count);
        Assert.Equal(2, repository.Assignments.Count);
    }

    private sealed class FakeRepository : IVerseRepository
    {
        public LearningPlan Plan { get; set; } = new(1, "student", 1, 1, DateTimeOffset.UtcNow, true);
        public IReadOnlyList<VerseProgress> Progress { get; set; } = [];
        public IReadOnlyList<Verse> Verses { get; set; } = [];
        public List<LessonAssignment> Assignments { get; } = [];
        private long _nextAssignmentId = 1;

        public string DatabasePath => string.Empty;
        public Task EnsureInitializedAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<int> GetVerseCountAsync(CancellationToken cancellationToken = default) => Task.FromResult(Verses.Count);
        public Task<Verse?> GetVerseAsync(int surahNum, int ayahNum, CancellationToken cancellationToken = default) => Task.FromResult(Verses.FirstOrDefault(v => v.SurahNum == surahNum && v.AyahNum == ayahNum));
        public Task<IReadOnlyList<Verse>> GetMemorizedVersesAsync(string? userKey = null, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Verse>>([]);
        public Task<IReadOnlyList<VerseProgress>> GetProgressAsync(string? userKey = null, CancellationToken cancellationToken = default) => Task.FromResult(Progress);
        public Task<IReadOnlyList<VerseProgress>> GetDueProgressAsync(string? userKey = null, DateTimeOffset? now = null, int limit = 20, CancellationToken cancellationToken = default) => Task.FromResult(Progress);
        public Task<IReadOnlyList<Verse>> GetAllVersesAsync(CancellationToken cancellationToken = default) => Task.FromResult(Verses);
        public Task<IReadOnlyList<Verse>> GetVersesByWordsAsync(IReadOnlyList<string> normalizedWords, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Verse>>([]);
        public Task<JuzInfo?> GetJuzForVerseAsync(int surahNum, int ayahNum, CancellationToken cancellationToken = default) => Task.FromResult<JuzInfo?>(null);
        public Task<JuzInfo?> GetJuzAsync(int juzNum, CancellationToken cancellationToken = default) => Task.FromResult<JuzInfo?>(null);
        public Task<SurahInfo?> GetSurahAsync(int surahNum, CancellationToken cancellationToken = default) => Task.FromResult<SurahInfo?>(null);
        public Task<IReadOnlyList<JuzInfo>> GetAllJuzAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<JuzInfo>>([]);
        public Task<IReadOnlyList<SurahInfo>> GetAllSurahsAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<SurahInfo>>([]);
        public Task<IReadOnlyList<SurahInfo>> GetSurahsByJuzAsync(int juzNum, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<SurahInfo>>([]);
        public Task<MushafPage?> GetPageAsync(int pageNum, CancellationToken cancellationToken = default) => Task.FromResult<MushafPage?>(null);
        public Task<int> GetPageCountAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<int> GetPageForVerseAsync(int surahNum, int ayahNum, CancellationToken cancellationToken = default) => Task.FromResult(1);
        public Task RecordRecitationAsync(string? userKey, int surahNum, int ayahNum, double masteryScore, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<LearningPlan> GetOrCreateLearningPlanAsync(string? userKey = null, LearningPlanInput? input = null, CancellationToken cancellationToken = default) => Task.FromResult(Plan);
        public Task<IReadOnlyList<LessonAssignment>> CreateAssignmentsAsync(string? userKey, IReadOnlyList<LessonAssignmentInput> inputs, CancellationToken cancellationToken = default)
        {
            foreach (var input in inputs)
            {
                if (Assignments.Any(a => a.SurahNum == input.SurahNum && a.AyahNum == input.AyahNum)) continue;
                Assignments.Add(new LessonAssignment(_nextAssignmentId++, Plan.Id, userKey ?? "student", input.SurahNum, input.AyahNum, input.Reason, LessonAssignmentStatus.Pending, input.DueAt ?? DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null));
            }
            return Task.FromResult<IReadOnlyList<LessonAssignment>>(Assignments);
        }
        public Task<IReadOnlyList<LessonAssignment>> GetAssignmentsAsync(string? userKey = null, LessonAssignmentStatus? status = null, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<LessonAssignment>>(Assignments.Where(a => status is null || a.Status == status).ToArray());
        public Task<LessonAssignment?> GetAssignmentAsync(long assignmentId, string? userKey = null, CancellationToken cancellationToken = default) => Task.FromResult(Assignments.FirstOrDefault(a => a.Id == assignmentId));
        public Task<LessonAssignment> MarkAssignmentInProgressAsync(long assignmentId, long sessionId, string? userKey = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<RecitationSession> OpenRecitationSessionAsync(string? userKey = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<RecitationSession> CloseRecitationSessionAsync(long sessionId, RecitationSessionStatus status = RecitationSessionStatus.Completed, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<RecitationSession>> GetRecitationSessionsAsync(string? userKey = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<VerseAttempt> SaveVerseAttemptAsync(string? userKey, VerseAttemptInput attempt, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<VerseAttempt>> GetVerseAttemptsAsync(string? userKey = null, long? sessionId = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<TajweedRuleSummary>> GetTajweedRuleSummariesAsync(string? userKey = null, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<TajweedRuleSummary>>([]);
        public Task<IReadOnlyList<TajweedErrorRecord>> GetTajweedErrorsAsync(string? userKey = null, TajweedRuleType? rule = null, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<TajweedErrorRecord>>([]);
    }
}
