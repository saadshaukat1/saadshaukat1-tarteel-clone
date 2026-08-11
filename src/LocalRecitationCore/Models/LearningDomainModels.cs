namespace TarteelClone.LocalRecitationCore.Models;

public enum LessonAssignmentReason
{
    NewLesson,
    Review
}

public enum LessonAssignmentStatus
{
    Pending,
    InProgress,
    Completed,
    Skipped
}

public enum RecitationSessionStatus
{
    Open,
    Completed,
    Abandoned
}

public sealed record LearningPlan(
    long Id,
    string UserKey,
    int DailyNewLessonTarget,
    int DailyReviewTarget,
    DateTimeOffset CreatedAt,
    bool IsActive);

public sealed record LessonAssignment(
    long Id,
    long PlanId,
    string UserKey,
    int SurahNum,
    int AyahNum,
    LessonAssignmentReason Reason,
    LessonAssignmentStatus Status,
    DateTimeOffset DueAt,
    DateTimeOffset CreatedAt,
    long? SessionId);

public sealed record RecitationSession(
    long Id,
    long PlanId,
    string UserKey,
    DateTimeOffset StartedAt,
    DateTimeOffset? ClosedAt,
    RecitationSessionStatus Status);

public sealed record VerseAttempt(
    long Id,
    long SessionId,
    long? AssignmentId,
    string UserKey,
    int SurahNum,
    int AyahNum,
    double MasteryScore,
    double Confidence,
    string TranscriptionText,
    DateTimeOffset AttemptedAt,
    IReadOnlyList<RecitationWordMismatch> Mismatches,
    IReadOnlyList<TajweedViolation> TajweedViolations);

public sealed record LearningPlanInput(
    int DailyNewLessonTarget = 1,
    int DailyReviewTarget = 5);

public sealed record LessonAssignmentInput(
    int SurahNum,
    int AyahNum,
    LessonAssignmentReason Reason,
    DateTimeOffset? DueAt = null);

public sealed record VerseAttemptInput(
    long SessionId,
    long? AssignmentId,
    int SurahNum,
    int AyahNum,
    double MasteryScore,
    double Confidence,
    string TranscriptionText,
    IReadOnlyList<RecitationWordMismatch> Mismatches,
    IReadOnlyList<TajweedViolation> TajweedViolations,
    DateTimeOffset? AttemptedAt = null);
