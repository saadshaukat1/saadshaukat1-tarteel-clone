using TarteelClone.LocalRecitationCore.Models;
using Xunit;

namespace TarteelMobile.Tests;

public sealed class ReviewSchedulerTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 10, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void CalculateNextReview_NewVerse_IsImmediatelyDue()
    {
        var scheduler = new ReviewScheduler();

        var next = scheduler.CalculateNextReview(0, 0, 0, Now);

        Assert.Equal(Now, next);
    }

    [Fact]
    public void CalculateNextReview_WeakPerformance_ReturnsShortInterval()
    {
        var scheduler = new ReviewScheduler();

        var next = scheduler.CalculateNextReview(0.82, 1, 4, Now);

        Assert.Equal(Now.AddHours(12), next);
    }

    [Fact]
    public void CalculateNextReview_MasteriedVerse_UsesAttemptInterval()
    {
        var scheduler = new ReviewScheduler();

        var next = scheduler.CalculateNextReview(0.95, 0, 3, Now);

        Assert.Equal(Now.AddDays(7), next);
    }

    [Fact]
    public void BuildDueQueue_PrioritizesWeakAndOverdueItems()
    {
        var scheduler = new ReviewScheduler();
        var progress = new[]
        {
            new ReviewProgress(2, 1, 0.90, Now.AddDays(-2), Now.AddDays(-2), 4, 0),
            new ReviewProgress(1, 7, 0.45, Now.AddHours(-4), Now.AddHours(-1), 3, 2),
            new ReviewProgress(1, 1, 0.0, null, null, 0, 0)
        };

        var queue = scheduler.BuildDueQueue(progress, Now);

        Assert.Equal(3, queue.Count);
        Assert.Equal((1, 7), (queue[0].SurahNum, queue[0].AyahNum));
        Assert.Equal(ReviewReason.WeakRecentPerformance, queue[0].Reason);
        Assert.Equal(ReviewReason.NewLesson, queue[1].Reason);
        Assert.Equal(ReviewReason.Overdue, queue[2].Reason);
    }

    [Fact]
    public void BuildDueQueue_ExcludesFutureReviewsAndHonorsLimit()
    {
        var scheduler = new ReviewScheduler();
        var progress = new[]
        {
            new ReviewProgress(1, 1, 0.8, Now, Now.AddHours(1), 2, 0),
            new ReviewProgress(1, 2, 0.8, Now, Now.AddHours(-1), 2, 0),
            new ReviewProgress(1, 3, 0.8, Now, Now.AddHours(-2), 2, 0)
        };

        var queue = scheduler.BuildDueQueue(progress, Now, limit: 1);

        Assert.Single(queue);
        Assert.Equal(3, queue[0].AyahNum);
    }
}
