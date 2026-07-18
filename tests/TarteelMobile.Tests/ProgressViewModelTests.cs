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

        var vm = new ProgressViewModel(repo, new FakeSessionService("hifz@example.com"), new FakeDiagnosticsService());

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
            new FakeSessionService("hifz@example.com"),
            new FakeDiagnosticsService());

        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.Equal("No verses practiced yet. Start reciting!", vm.OverallSummary);
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

        public Task RecordRecitationAsync(string? userKey, int surahNum, int ayahNum, double masteryScore, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
        public Task<IReadOnlyList<Verse>> GetAllVersesAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Verse>>([]);
        public Task<IReadOnlyList<Verse>> GetVersesByWordsAsync(IReadOnlyList<string> w, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Verse>>([]);
        public Task<JuzInfo?> GetJuzForVerseAsync(int s, int a, CancellationToken ct = default) => Task.FromResult<JuzInfo?>(null);
        public Task<JuzInfo?> GetJuzAsync(int j, CancellationToken ct = default) => Task.FromResult<JuzInfo?>(null);
        public Task<SurahInfo?> GetSurahAsync(int s, CancellationToken ct = default) => Task.FromResult<SurahInfo?>(null);
        public Task<IReadOnlyList<JuzInfo>> GetAllJuzAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<JuzInfo>>([]);
        public Task<IReadOnlyList<SurahInfo>> GetAllSurahsAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<SurahInfo>>([]);
        public Task<IReadOnlyList<SurahInfo>> GetSurahsByJuzAsync(int j, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<SurahInfo>>([]);
        public Task<int> GetPageCountAsync(CancellationToken ct = default) => Task.FromResult(0);
        public Task<MushafPage?> GetPageAsync(int pageNum, CancellationToken ct = default) => Task.FromResult<MushafPage?>(null);
        public Task<int> GetPageForVerseAsync(int s, int a, CancellationToken ct = default) => Task.FromResult(1);
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
        public Task<IReadOnlyList<Verse>> GetAllVersesAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Verse>>([]);
        public Task<IReadOnlyList<Verse>> GetVersesByWordsAsync(IReadOnlyList<string> w, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Verse>>([]);
        public Task<JuzInfo?> GetJuzForVerseAsync(int s, int a, CancellationToken ct = default) => Task.FromResult<JuzInfo?>(null);
        public Task<JuzInfo?> GetJuzAsync(int j, CancellationToken ct = default) => Task.FromResult<JuzInfo?>(null);
        public Task<SurahInfo?> GetSurahAsync(int s, CancellationToken ct = default) => Task.FromResult<SurahInfo?>(null);
        public Task<IReadOnlyList<JuzInfo>> GetAllJuzAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<JuzInfo>>([]);
        public Task<IReadOnlyList<SurahInfo>> GetAllSurahsAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<SurahInfo>>([]);
        public Task<IReadOnlyList<SurahInfo>> GetSurahsByJuzAsync(int j, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<SurahInfo>>([]);
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
