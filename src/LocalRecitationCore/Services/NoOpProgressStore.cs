using TarteelClone.LocalRecitationCore.Abstractions;
using TarteelClone.LocalRecitationCore.Models;

namespace TarteelClone.LocalRecitationCore.Services;

/// <summary>
/// Baseline progress store for Phase 1; replaced by sqlite implementation later.
/// </summary>
public sealed class NoOpProgressStore : IProgressStore
{
    public Task SaveAsync(RecitationMatchResult result, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
