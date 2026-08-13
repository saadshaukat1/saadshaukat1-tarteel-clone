using TarteelClone.LocalRecitationCore.Models;
using TarteelClone.UserService;
using TarteelMobile.Models;
using TarteelMobile.Services;
using TarteelMobile.ViewModels;
using Xunit;

namespace TarteelMobile.Tests;

public sealed class ProgressViewModelTests
{
    [Fact]
    public async Task Refresh_WithResults_UsesAsciiSeparators()
    {
        var repo = new FakeVerseRepository([
            new VerseProgress(1, 1, "text", 0.95, DateTimeOffset.UtcNow),
            new VerseProgress(1, 2, "text", 0.50, DateTimeOffset.UtcNow)
        ]);

        var vm = new ProgressViewModel(repo, new FakeTodayWorkflowService(), new FakeSessionService("hifz@example.com"), new FakeDiagnosticsService(), new FakeCurriculumService());

        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.Contains("|", vm.OverallSummary);
        Assert.DoesNotContain("�", vm.OverallSummary);
        Assert.Contains("verse(s) practiced", vm.OverallSummary);
        Assert.Contains("mastered", vm.OverallSummary);
        Assert.Contains("Avg accuracy", vm.OverallSummary);
    }

    [Fact]
    public async Task Refresh_WithNoResults_ShowsEmptyStateSummary()
    {
        var vm = new ProgressViewModel(
            new FakeVerseRepository([]),
            new FakeTodayWorkflowService(),
            new FakeSessionService("hifz@example.com"),
            new FakeDiagnosticsService(),
            new FakeCurriculumService());

        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.Equal("No verses practiced yet. Start reciting!", vm.OverallSummary);
    }

    private sealed class FakeCurriculumService : ICurriculumService
    {
        public IReadOnlyList<(int SurahNum, int AyahNum)> GetLearningPath(CurriculumPath path = CurriculumPath.Juz30)
            => [(1, 1), (1, 2)];
    }

    private sealed class FakeTodayWorkflowService : ITodayWorkflowService
    {
        public Task<IReadOnlyList<TodayAssignment>> GetTodayAssignmentsAsync(string? userKey = null, DateTimeOffset? now = null, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<TodayAssignment>>([]);
    }

    private sealed class FakeVerseRepository : IVerseRepository
    {
        private readonly IReadOnlyList<VerseProgress> _progress;

        public FakeVerseRepository(IReadOnlyList<VerseProgress> progress)
        {
            _progress = progress;
        }

        public string DatabasePath => "";
        public Task EnsureInitializedAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<int> GetVerseCountAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<Verse?> GetVerseAsync(int surahNum, int ayahNum, CancellationToken cancellationToken = default)
            => Task.FromResult<Verse?>(null);

        public Task<IReadOnlyList<Verse>> GetMemorizedVersesAsync(string? userKey = null, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Verse>>([]);

        public Task<IReadOnlyList<VerseProgress>> GetProgressAsync(string? userKey = null, CancellationToken cancellationToken = default)
            => Task.FromResult(_progress);

        public Task<IReadOnlyList<VerseProgress>> GetDueProgressAsync(string? userKey = null, DateTimeOffset? now = null, int limit = 20, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<VerseProgress>>(_progress.Take(Math.Max(limit, 0)).ToArray());

        public Task RecordRecitationAsync(string? userKey, int surahNum, int ayahNum, double masteryScore, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
        public Task<LearningPlan> GetOrCreateLearningPlanAsync(string? userKey = null, LearningPlanInput? input = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<LessonAssignment>> CreateAssignmentsAsync(string? userKey, IReadOnlyList<LessonAssignmentInput> assignments, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<LessonAssignment>> GetAssignmentsAsync(string? userKey = null, LessonAssignmentStatus? status = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<LessonAssignment?> GetAssignmentAsync(long assignmentId, string? userKey = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<LessonAssignment> MarkAssignmentInProgressAsync(long assignmentId, long sessionId, string? userKey = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<RecitationSession> OpenRecitationSessionAsync(string? userKey = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<RecitationSession> CloseRecitationSessionAsync(long sessionId, RecitationSessionStatus status = RecitationSessionStatus.Completed, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<RecitationSession>> GetRecitationSessionsAsync(string? userKey = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<VerseAttempt> SaveVerseAttemptAsync(string? userKey, VerseAttemptInput attempt, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<VerseAttempt>> GetVerseAttemptsAsync(string? userKey = null, long? sessionId = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<Verse>> GetAllVersesAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Verse>>([]);
        public Task<IReadOnlyList<Verse>> GetVersesByWordsAsync(IReadOnlyList<string> w, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Verse>>([]);
        public Task<IReadOnlyList<Verse>> GetVersesBySearchAsync(string query, int maxResults = 20, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Verse>>([]);
        public Task<JuzInfo?> GetJuzForVerseAsync(int s, int a, CancellationToken ct = default) => Task.FromResult<JuzInfo?>(null);
        public Task<JuzInfo?> GetJuzAsync(int j, CancellationToken ct = default) => Task.FromResult<JuzInfo?>(null);
        public Task<SurahInfo?> GetSurahAsync(int s, CancellationToken ct = default) => Task.FromResult<SurahInfo?>(null);
        public Task<IReadOnlyList<JuzInfo>> GetAllJuzAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<JuzInfo>>([]);
        public Task<IReadOnlyList<SurahInfo>> GetAllSurahsAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<SurahInfo>>([]);
        public Task<IReadOnlyList<SurahInfo>> GetSurahsByJuzAsync(int j, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<SurahInfo>>([]);
        public Task<int> GetPageCountAsync(CancellationToken ct = default) => Task.FromResult(0);
        public Task<MushafPage?> GetPageAsync(int pageNum, CancellationToken ct = default) => Task.FromResult<MushafPage?>(null);
        public Task<int> GetPageForVerseAsync(int s, int a, CancellationToken ct = default) => Task.FromResult(1);
        public Task<IReadOnlyList<TajweedRuleSummary>> GetTajweedRuleSummariesAsync(string? userKey = null, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<TajweedRuleSummary>>([]);
        public Task<IReadOnlyList<TajweedErrorRecord>> GetTajweedErrorsAsync(string? userKey = null, TajweedRuleType? rule = null, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<TajweedErrorRecord>>([]);
        public Task<(CurriculumPath Path, int Position)> GetCurriculumPositionAsync(string? userKey = null, CancellationToken cancellationToken = default) => Task.FromResult((CurriculumPath.Juz30, 0));
        public Task SetCurriculumPositionAsync(CurriculumPath path, int position, string? userKey = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RecordPracticeDayAsync(string? userKey = null, DateTimeOffset? day = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<PracticeStreak> GetStreakAsync(string? userKey = null, DateTimeOffset? now = null, CancellationToken cancellationToken = default) => Task.FromResult(new PracticeStreak(0, 0, 0));
        public Task<UserProfile?> GetUserProfileAsync(string userKey, CancellationToken cancellationToken = default) => Task.FromResult<UserProfile?>(null);
        public Task<UserProfile> CreateUserProfileAsync(UserProfile profile, CancellationToken cancellationToken = default) => Task.FromResult(profile);
        public Task<UserPreferences> GetUserPreferencesAsync(string userKey, CancellationToken cancellationToken = default) => Task.FromResult(new UserPreferences(userKey, MemorizationGoal.Casual, 1, 5, false, DateTimeOffset.UtcNow));
        public Task<UserPreferences> SaveUserPreferencesAsync(UserPreferences preferences, CancellationToken cancellationToken = default) => Task.FromResult(preferences);
        public Task<IReadOnlyList<WeakVerseRecommendation>> GetWeakVerseRecommendationsAsync(string? userKey = null, int limit = 10, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<WeakVerseRecommendation>>([]);
    }

    private sealed class FakeSessionService : ISessionService
    {
        public FakeSessionService(string email)
        {
            CurrentUserEmail = email;
        }

        public bool IsAuthenticated => true;
        public string? CurrentUserEmail { get; }
        public Task<bool> LoginAsync(string email, string password) => Task.FromResult(true);
        public Task<bool> RegisterAsync(string email, string password) => Task.FromResult(true);
        public Task LogoutAsync() => Task.CompletedTask;
    }

    private sealed class FakeDiagnosticsService : IAppDiagnosticsService
    {
        public string LogPath => "";
        public void Info(string message) { }
        public void Warn(string message) { }
        public void Error(string message, Exception? exception = null) { }
        public Task<IReadOnlyList<string>> ReadRecentAsync(int maxLines = 200) => Task.FromResult<IReadOnlyList<string>>([]);
    }
}
