using TarteelClone.LocalRecitationCore.Abstractions;
using TarteelClone.LocalRecitationCore.Models;
using TarteelClone.LocalRecitationCore.Services;
using Xunit;

namespace TarteelMobile.Tests;

public sealed class RecitationPipelineTests
{
    [Fact]
    public async Task Orchestrator_DoesNotPersistOrEmitWeakVerseMatch()
    {
        var progressStore = new CountingProgressStore();
        var matcher = new FixedVerseMatcher(new RecitationMatchResult
        {
            SurahNum = 4,
            AyahNum = 174,
            ArabicText = "الحمد لله رب العالمين",
            Confidence = 0.39,
            MatchedWordCount = 1,
            ProcessedWordCount = 4
        });
        var orchestrator = new OfflineRecitationOrchestrator(
            new FixedAsrEngine("يا ايها الناس"), matcher, progressStore);
        var matchEmitted = false;
        orchestrator.MatchProduced += (_, _) => matchEmitted = true;

        await orchestrator.StartAsync();
        await orchestrator.SubmitAudioChunkAsync(new byte[] { 1, 0, 1, 0 });

        Assert.Equal(0, progressStore.SaveCount);
        Assert.False(matchEmitted);
    }

    [Fact]
    public async Task Orchestrator_PersistsAndEmitsReliableVerseMatch()
    {
        var progressStore = new CountingProgressStore();
        var matcher = new FixedVerseMatcher(new RecitationMatchResult
        {
            SurahNum = 1,
            AyahNum = 1,
            ArabicText = "بسم الله الرحمن الرحيم",
            Confidence = 0.9,
            MatchedWordCount = 4,
            ProcessedWordCount = 4
        });
        var orchestrator = new OfflineRecitationOrchestrator(
            new FixedAsrEngine("بسم الله الرحمن الرحيم"), matcher, progressStore);
        var matchEmitted = false;
        orchestrator.MatchProduced += (_, _) => matchEmitted = true;

        await orchestrator.StartAsync();
        await orchestrator.SubmitAudioChunkAsync(new byte[] { 1, 0, 1, 0 });

        Assert.Equal(1, progressStore.SaveCount);
        Assert.True(matchEmitted);
    }

    [Fact]
    public async Task Matcher_DoesNotEscapeSelectedSurahWhenCandidatesAreMissing()
    {
        var repository = new InMemoryVerseRepository(
        [
            new RecitationVerse
            {
                SurahNum = 4,
                AyahNum = 174,
                ArabicText = "يا ايها الناس اتقوا ربكم"
            }
        ]);
        var matcher = new LocalVerseMatcher(repository);
        matcher.SetSurahContext(1);

        var result = await matcher.MatchAsync("يا ايها الناس اتقوا ربكم");

        Assert.Equal(0, result.SurahNum);
        Assert.Equal(0, result.Confidence);
    }

    private sealed class CountingProgressStore : IProgressStore
    {
        public int SaveCount { get; private set; }

        public Task SaveAsync(RecitationMatchResult result, CancellationToken cancellationToken = default)
        {
            SaveCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class FixedAsrEngine(string text) : IAsrEngine
    {
        public bool IsReady { get; private set; }
        public string ActiveTier => "test";

        public Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            IsReady = true;
            return Task.CompletedTask;
        }

        public Task<RecitationTranscriptionResult> TranscribeAsync(
            byte[] audioChunk,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new RecitationTranscriptionResult(true, text, 0f, ActiveTier, false));

        public void SetSurahPrompt(string surahArabicName) { }
        public void ClearSurahPrompt() { }
    }

    private sealed class FixedVerseMatcher(RecitationMatchResult result) : IVerseMatcher
    {
        public void SetSurahContext(int surahNumber) { }
        public void ClearSurahContext() { }
        public void SetLastMatchedPosition(int surahNum, int ayahNum) { }
        public void ClearLastMatchedPosition() { }
        public void MarkCurrentAyahComplete() { }

        public Task<RecitationMatchResult> MatchAsync(
            string arabicText,
            CancellationToken cancellationToken = default) => Task.FromResult(result);
    }

    private sealed class InMemoryVerseRepository(IReadOnlyList<RecitationVerse> verses) : IVerseRepository
    {
        public Task<RecitationVerse?> GetVerseAsync(
            int surahNum,
            int ayahNum,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(verses.FirstOrDefault(v => v.SurahNum == surahNum && v.AyahNum == ayahNum));

        public Task<IReadOnlyList<RecitationVerse>> GetMemorizedVersesAsync(
            CancellationToken cancellationToken = default) => Task.FromResult(verses);

        public Task<IReadOnlyList<RecitationVerse>> GetAllVersesAsync(
            CancellationToken cancellationToken = default) => Task.FromResult(verses);

        public Task<IReadOnlyList<RecitationVerse>> GetCandidateVersesAsync(
            IReadOnlyList<string> normalizedWords,
            CancellationToken cancellationToken = default) => Task.FromResult(verses);
    }
}
