namespace TarteelClone.LocalRecitationCore.Models;

public enum ReviewReason
{
    NewLesson,
    DueToday,
    WeakRecentPerformance,
    Overdue
}

public sealed record ReviewItem(
    int SurahNum,
    int AyahNum,
    double MasteryScore,
    DateTimeOffset? NextReviewAt,
    ReviewReason Reason,
    int Priority)
{
    public bool IsDue(DateTimeOffset now) => NextReviewAt is null || NextReviewAt <= now;
}

public sealed record ReviewPolicy(
    IReadOnlyList<TimeSpan> MasteryIntervals,
    TimeSpan WeakPerformanceInterval,
    TimeSpan OverdueGracePeriod)
{
    public static ReviewPolicy Default { get; } = new(
        [
            TimeSpan.Zero,
            TimeSpan.FromDays(1),
            TimeSpan.FromDays(3),
            TimeSpan.FromDays(7),
            TimeSpan.FromDays(14),
            TimeSpan.FromDays(30)
        ],
        TimeSpan.FromHours(12),
        TimeSpan.FromDays(1));
}

public sealed record ReviewProgress(
    int SurahNum,
    int AyahNum,
    double MasteryScore,
    DateTimeOffset? LastPracticedAt,
    DateTimeOffset? NextReviewAt,
    int AttemptCount,
    int RecentErrorCount)
{
    public bool IsDue(DateTimeOffset now) => NextReviewAt is null || NextReviewAt <= now;
}

public sealed class ReviewScheduler
{
    private readonly ReviewPolicy _policy;

    public ReviewScheduler(ReviewPolicy? policy = null)
    {
        _policy = policy ?? ReviewPolicy.Default;
    }

    public DateTimeOffset CalculateNextReview(
        double masteryScore,
        int recentErrorCount,
        int attemptCount,
        DateTimeOffset now)
    {
        if (attemptCount == 0)
        {
            return now;
        }

        if (recentErrorCount > 0 || masteryScore < 0.6)
        {
            return now.Add(_policy.WeakPerformanceInterval);
        }

        var intervalIndex = Math.Clamp(attemptCount, 0, _policy.MasteryIntervals.Count - 1);
        return now.Add(_policy.MasteryIntervals[intervalIndex]);
    }

    public IReadOnlyList<ReviewItem> BuildDueQueue(
        IEnumerable<ReviewProgress> progress,
        DateTimeOffset now,
        int limit = 20)
    {
        return progress
            .Where(item => item.IsDue(now))
            .Select(item => new ReviewItem(
                item.SurahNum,
                item.AyahNum,
                item.MasteryScore,
                item.NextReviewAt,
                GetReason(item, now),
                GetPriority(item, now)))
            .OrderByDescending(item => item.Priority)
            .ThenBy(item => item.NextReviewAt ?? DateTimeOffset.MinValue)
            .ThenBy(item => item.SurahNum)
            .ThenBy(item => item.AyahNum)
            .Take(Math.Max(limit, 0))
            .ToArray();
    }

    private ReviewReason GetReason(ReviewProgress item, DateTimeOffset now)
    {
        if (item.AttemptCount == 0)
        {
            return ReviewReason.NewLesson;
        }

        if (item.RecentErrorCount > 0 || item.MasteryScore < 0.6)
        {
            return ReviewReason.WeakRecentPerformance;
        }

        if (item.NextReviewAt is { } nextReview && nextReview < now - _policy.OverdueGracePeriod)
        {
            return ReviewReason.Overdue;
        }

        return ReviewReason.DueToday;
    }

    private int GetPriority(ReviewProgress item, DateTimeOffset now)
    {
        var priority = 0;
        if (item.RecentErrorCount > 0)
        {
            priority += 50 + Math.Min(item.RecentErrorCount, 10);
        }

        if (item.MasteryScore < 0.6)
        {
            priority += 30;
        }

        if (item.NextReviewAt is null || item.NextReviewAt < now - _policy.OverdueGracePeriod)
        {
            priority += 20;
        }

        if (item.AttemptCount == 0)
        {
            priority += 10;
        }

        return priority;
    }
}
