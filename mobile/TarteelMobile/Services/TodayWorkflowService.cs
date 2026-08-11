using TarteelClone.LocalRecitationCore.Models;
using TarteelMobile.Models;

namespace TarteelMobile.Services;

public sealed record TodayAssignment(
    LessonAssignment Assignment,
    Verse Verse,
    string ReasonDisplay,
    int Priority);

public interface ITodayWorkflowService
{
    Task<IReadOnlyList<TodayAssignment>> GetTodayAssignmentsAsync(
        string? userKey = null,
        DateTimeOffset? now = null,
        CancellationToken cancellationToken = default);
}

public sealed class TodayWorkflowService : ITodayWorkflowService
{
    private readonly IVerseRepository _verses;

    public TodayWorkflowService(IVerseRepository verses)
    {
        _verses = verses;
    }

    public async Task<IReadOnlyList<TodayAssignment>> GetTodayAssignmentsAsync(
        string? userKey = null,
        DateTimeOffset? now = null,
        CancellationToken cancellationToken = default)
    {
        var currentTime = now ?? DateTimeOffset.UtcNow;
        var plan = await _verses.GetOrCreateLearningPlanAsync(userKey, cancellationToken: cancellationToken);
        var progress = await _verses.GetProgressAsync(userKey, cancellationToken);
        var existingAssignments = await _verses.GetAssignmentsAsync(userKey, cancellationToken: cancellationToken);
        var activeAssignments = existingAssignments
            .Where(assignment => assignment.Status is LessonAssignmentStatus.Pending or LessonAssignmentStatus.InProgress)
            .ToList();
        var knownKeys = existingAssignments
            .Select(assignment => (assignment.SurahNum, assignment.AyahNum))
            .ToHashSet();

        var dueReviewKeys = new ReviewScheduler()
            .BuildDueQueue(
                progress.Select(item => new ReviewProgress(
                    item.SurahNum,
                    item.AyahNum,
                    item.MasteryScore,
                    item.UpdatedAt,
                    item.NextReviewAt,
                    item.AttemptCount,
                    item.RecentErrorCount)),
                currentTime,
                Math.Max(plan.DailyReviewTarget, 0))
            .Select(item => (item.SurahNum, item.AyahNum))
            .Where(key => !knownKeys.Contains(key))
            .ToArray();

        var activeReviewCount = activeAssignments.Count(item => item.Reason == LessonAssignmentReason.Review);
        var reviewSlots = Math.Max(plan.DailyReviewTarget - activeReviewCount, 0);
        if (reviewSlots > 0 && dueReviewKeys.Length > 0)
        {
            await _verses.CreateAssignmentsAsync(
                userKey,
                dueReviewKeys
                    .Take(reviewSlots)
                    .Select(key => new LessonAssignmentInput(key.SurahNum, key.AyahNum, LessonAssignmentReason.Review, currentTime))
                    .ToArray(),
                cancellationToken);
        }

        var refreshedAssignments = await _verses.GetAssignmentsAsync(userKey, cancellationToken: cancellationToken);
        activeAssignments = refreshedAssignments
            .Where(assignment => assignment.Status is LessonAssignmentStatus.Pending or LessonAssignmentStatus.InProgress)
            .ToList();
        knownKeys = refreshedAssignments
            .Select(assignment => (assignment.SurahNum, assignment.AyahNum))
            .ToHashSet();

        var activeNewLessonCount = activeAssignments.Count(item => item.Reason == LessonAssignmentReason.NewLesson);
        var newLessonSlots = Math.Max(plan.DailyNewLessonTarget - activeNewLessonCount, 0);
        if (newLessonSlots > 0)
        {
            var progressKeys = progress
                .Select(item => (item.SurahNum, item.AyahNum))
                .ToHashSet();
            var newLessonKeys = (await _verses.GetAllVersesAsync(cancellationToken))
                .Where(verse => !progressKeys.Contains((verse.SurahNum, verse.AyahNum)))
                .Where(verse => !knownKeys.Contains((verse.SurahNum, verse.AyahNum)))
                .Take(newLessonSlots)
                .Select(verse => (verse.SurahNum, verse.AyahNum))
                .ToArray();
            if (newLessonKeys.Length > 0)
            {
                await _verses.CreateAssignmentsAsync(
                    userKey,
                    newLessonKeys.Select(key => new LessonAssignmentInput(key.SurahNum, key.AyahNum, LessonAssignmentReason.NewLesson, currentTime)).ToArray(),
                    cancellationToken);
            }
        }

        refreshedAssignments = await _verses.GetAssignmentsAsync(userKey, cancellationToken: cancellationToken);
        var versesByKey = (await _verses.GetAllVersesAsync(cancellationToken))
            .ToDictionary(verse => (verse.SurahNum, verse.AyahNum));
        return refreshedAssignments
            .Where(assignment => assignment.Status is LessonAssignmentStatus.Pending or LessonAssignmentStatus.InProgress)
            .Where(assignment => versesByKey.ContainsKey((assignment.SurahNum, assignment.AyahNum)))
            .OrderBy(assignment => assignment.Status == LessonAssignmentStatus.InProgress ? 0 : 1)
            .ThenBy(assignment => assignment.Reason == LessonAssignmentReason.Review ? 0 : 1)
            .ThenBy(assignment => assignment.DueAt)
            .ThenBy(assignment => assignment.SurahNum)
            .ThenBy(assignment => assignment.AyahNum)
            .Select((assignment, index) => new TodayAssignment(
                assignment,
                versesByKey[(assignment.SurahNum, assignment.AyahNum)],
                assignment.Reason == LessonAssignmentReason.Review ? "Review due" : "New lesson",
                index + 1))
            .ToArray();
    }
}
