using Microsoft.Data.Sqlite;
using TarteelClone.LocalRecitationCore.Models;

namespace TarteelMobile.Services;

public partial class LocalVerseRepository
{
    public async Task<LearningPlan> GetOrCreateLearningPlanAsync(
        string? userKey = null,
        LearningPlanInput? input = null,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        var normalizedUserKey = ResolveUserKey(userKey);
        var settings = input ?? new LearningPlanInput();
        var now = DateTimeOffset.UtcNow;
        await using var connection = CreateOpenConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT OR IGNORE INTO learning_plans (
                user_key, daily_new_lesson_target, daily_review_target, created_at, is_active)
            VALUES (@user_key, @new_target, @review_target, @created_at, 1);
            """;
        command.Parameters.AddWithValue("@user_key", normalizedUserKey);
        command.Parameters.AddWithValue("@new_target", Math.Max(settings.DailyNewLessonTarget, 0));
        command.Parameters.AddWithValue("@review_target", Math.Max(settings.DailyReviewTarget, 0));
        command.Parameters.AddWithValue("@created_at", now.ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken);
        return await ReadLearningPlanAsync(connection, normalizedUserKey, cancellationToken)
            ?? throw new InvalidOperationException("Learning plan could not be created.");
    }

    public async Task<IReadOnlyList<LessonAssignment>> CreateAssignmentsAsync(
        string? userKey,
        IReadOnlyList<LessonAssignmentInput> assignments,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        var normalizedUserKey = ResolveUserKey(userKey);
        var plan = await GetOrCreateLearningPlanAsync(normalizedUserKey, cancellationToken: cancellationToken);
        await using var connection = CreateOpenConnection();
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        foreach (var assignment in assignments)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                INSERT OR IGNORE INTO lesson_assignments (
                    plan_id, user_key, surah_num, ayah_num, reason, status, due_at, created_at)
                VALUES (@plan_id, @user_key, @surah_num, @ayah_num, @reason, @status, @due_at, @created_at);
                """;
            var dueAt = assignment.DueAt ?? DateTimeOffset.UtcNow;
            command.Parameters.AddWithValue("@plan_id", plan.Id);
            command.Parameters.AddWithValue("@user_key", normalizedUserKey);
            command.Parameters.AddWithValue("@surah_num", assignment.SurahNum);
            command.Parameters.AddWithValue("@ayah_num", assignment.AyahNum);
            command.Parameters.AddWithValue("@reason", (int)assignment.Reason);
            command.Parameters.AddWithValue("@status", (int)LessonAssignmentStatus.Pending);
            command.Parameters.AddWithValue("@due_at", dueAt.ToString("O"));
            command.Parameters.AddWithValue("@created_at", DateTimeOffset.UtcNow.ToString("O"));
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
        return await GetAssignmentsAsync(normalizedUserKey, cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<LessonAssignment>> GetAssignmentsAsync(
        string? userKey = null,
        LessonAssignmentStatus? status = null,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        await using var connection = CreateOpenConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, plan_id, user_key, surah_num, ayah_num, reason, status, due_at, created_at, session_id
            FROM lesson_assignments
            WHERE user_key = @user_key
              AND (@status IS NULL OR status = @status)
            ORDER BY due_at, id;
            """;
        command.Parameters.AddWithValue("@user_key", ResolveUserKey(userKey));
        command.Parameters.AddWithValue("@status", status is null ? DBNull.Value : (object)(int)status.Value);
        var results = new List<LessonAssignment>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(ReadAssignment(reader));
        }
        return results;
    }

    public async Task<LessonAssignment?> GetAssignmentAsync(
        long assignmentId,
        string? userKey = null,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        await using var connection = CreateOpenConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, plan_id, user_key, surah_num, ayah_num, reason, status, due_at, created_at, session_id
            FROM lesson_assignments
            WHERE id = @id AND user_key = @user_key
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("@id", assignmentId);
        command.Parameters.AddWithValue("@user_key", ResolveUserKey(userKey));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadAssignment(reader) : null;
    }

    public async Task<LessonAssignment> MarkAssignmentInProgressAsync(
        long assignmentId,
        long sessionId,
        string? userKey = null,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        await using var connection = CreateOpenConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE lesson_assignments
            SET status = @status, session_id = @session_id
            WHERE id = @id AND user_key = @user_key AND status IN (@pending, @in_progress);
            """;
        command.Parameters.AddWithValue("@status", (int)LessonAssignmentStatus.InProgress);
        command.Parameters.AddWithValue("@session_id", sessionId);
        command.Parameters.AddWithValue("@id", assignmentId);
        command.Parameters.AddWithValue("@user_key", ResolveUserKey(userKey));
        command.Parameters.AddWithValue("@pending", (int)LessonAssignmentStatus.Pending);
        command.Parameters.AddWithValue("@in_progress", (int)LessonAssignmentStatus.InProgress);
        if (await command.ExecuteNonQueryAsync(cancellationToken) == 0)
        {
            throw new InvalidOperationException($"Assignment {assignmentId} is not available for this session.");
        }

        return await GetAssignmentAsync(assignmentId, userKey, cancellationToken)
            ?? throw new InvalidOperationException($"Assignment {assignmentId} could not be read.");
    }

    public async Task<RecitationSession> OpenRecitationSessionAsync(
        string? userKey = null,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        var normalizedUserKey = ResolveUserKey(userKey);
        var plan = await GetOrCreateLearningPlanAsync(normalizedUserKey, cancellationToken: cancellationToken);
        var startedAt = DateTimeOffset.UtcNow;
        await using var connection = CreateOpenConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO recitation_sessions (plan_id, user_key, started_at, status)
            VALUES (@plan_id, @user_key, @started_at, @status);
            SELECT last_insert_rowid();
            """;
        command.Parameters.AddWithValue("@plan_id", plan.Id);
        command.Parameters.AddWithValue("@user_key", normalizedUserKey);
        command.Parameters.AddWithValue("@started_at", startedAt.ToString("O"));
        command.Parameters.AddWithValue("@status", (int)RecitationSessionStatus.Open);
        var id = Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken));
        return new RecitationSession(id, plan.Id, normalizedUserKey, startedAt, null, RecitationSessionStatus.Open);
    }

    public async Task<RecitationSession> CloseRecitationSessionAsync(
        long sessionId,
        RecitationSessionStatus status = RecitationSessionStatus.Completed,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        var closedAt = DateTimeOffset.UtcNow;
        await using var connection = CreateOpenConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE recitation_sessions
            SET closed_at = @closed_at, status = @status
            WHERE id = @id AND status = @open_status;
            """;
        command.Parameters.AddWithValue("@closed_at", closedAt.ToString("O"));
        command.Parameters.AddWithValue("@status", (int)status);
        command.Parameters.AddWithValue("@id", sessionId);
        command.Parameters.AddWithValue("@open_status", (int)RecitationSessionStatus.Open);
        if (await command.ExecuteNonQueryAsync(cancellationToken) == 0)
        {
            throw new InvalidOperationException($"Open recitation session {sessionId} was not found.");
        }
        return await ReadSessionAsync(connection, sessionId, cancellationToken)
            ?? throw new InvalidOperationException($"Recitation session {sessionId} could not be read.");
    }

    public async Task<IReadOnlyList<RecitationSession>> GetRecitationSessionsAsync(
        string? userKey = null,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        await using var connection = CreateOpenConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, plan_id, user_key, started_at, closed_at, status FROM recitation_sessions WHERE user_key = @user_key ORDER BY started_at, id;";
        command.Parameters.AddWithValue("@user_key", ResolveUserKey(userKey));
        var sessions = new List<RecitationSession>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            sessions.Add(new RecitationSession(
                reader.GetInt64(0), reader.GetInt64(1), reader.GetString(2), DateTimeOffset.Parse(reader.GetString(3)),
                reader.IsDBNull(4) ? null : DateTimeOffset.Parse(reader.GetString(4)),
                (RecitationSessionStatus)reader.GetInt32(5)));
        }
        return sessions;
    }

    public async Task<VerseAttempt> SaveVerseAttemptAsync(
        string? userKey,
        VerseAttemptInput attempt,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        var normalizedUserKey = ResolveUserKey(userKey);
        var score = Math.Clamp(attempt.MasteryScore, 0.0, 1.0);
        var confidence = Math.Clamp(attempt.Confidence, 0.0, 1.0);
        var attemptedAt = attempt.AttemptedAt ?? DateTimeOffset.UtcNow;
        await using var connection = CreateOpenConnection();
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        await EnsureSessionOwnershipAsync(connection, transaction, attempt.SessionId, normalizedUserKey, cancellationToken);

        await using var insert = connection.CreateCommand();
        insert.Transaction = transaction;
        insert.CommandText = """
            INSERT INTO verse_attempts (
                session_id, assignment_id, user_key, surah_num, ayah_num, mastery_score,
                confidence, transcription_text, attempted_at)
            VALUES (@session_id, @assignment_id, @user_key, @surah_num, @ayah_num, @mastery_score,
                @confidence, @transcription_text, @attempted_at);
            SELECT last_insert_rowid();
            """;
        insert.Parameters.AddWithValue("@session_id", attempt.SessionId);
        insert.Parameters.AddWithValue("@assignment_id", attempt.AssignmentId is null ? DBNull.Value : (object)attempt.AssignmentId.Value);
        insert.Parameters.AddWithValue("@user_key", normalizedUserKey);
        insert.Parameters.AddWithValue("@surah_num", attempt.SurahNum);
        insert.Parameters.AddWithValue("@ayah_num", attempt.AyahNum);
        insert.Parameters.AddWithValue("@mastery_score", score);
        insert.Parameters.AddWithValue("@confidence", confidence);
        insert.Parameters.AddWithValue("@transcription_text", attempt.TranscriptionText ?? string.Empty);
        insert.Parameters.AddWithValue("@attempted_at", attemptedAt.ToString("O"));
        var attemptId = Convert.ToInt64(await insert.ExecuteScalarAsync(cancellationToken));

        foreach (var mismatch in attempt.Mismatches)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "INSERT INTO attempt_mismatches (attempt_id, position, spoken, expected) VALUES (@attempt_id, @position, @spoken, @expected);";
            command.Parameters.AddWithValue("@attempt_id", attemptId);
            command.Parameters.AddWithValue("@position", mismatch.Position);
            command.Parameters.AddWithValue("@spoken", mismatch.Spoken);
            command.Parameters.AddWithValue("@expected", mismatch.Expected);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        foreach (var violation in attempt.TajweedViolations)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "INSERT INTO attempt_tajweed_errors (attempt_id, word_position, rule, expected_word, spoken_word, hint) VALUES (@attempt_id, @word_position, @rule, @expected_word, @spoken_word, @hint);";
            command.Parameters.AddWithValue("@attempt_id", attemptId);
            command.Parameters.AddWithValue("@word_position", violation.WordPosition);
            command.Parameters.AddWithValue("@rule", (int)violation.Rule);
            command.Parameters.AddWithValue("@expected_word", violation.ExpectedWord);
            command.Parameters.AddWithValue("@spoken_word", violation.SpokenWord);
            command.Parameters.AddWithValue("@hint", violation.Hint);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        foreach (var violation in attempt.TajweedViolations)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO tajweed_error_history (user_key, surah_num, ayah_num, rule, error_count, last_attempted_at)
                VALUES (@user_key, @surah_num, @ayah_num, @rule, 1, @attempted_at)
                ON CONFLICT(user_key, surah_num, ayah_num, rule) DO UPDATE SET
                    error_count = tajweed_error_history.error_count + 1,
                    last_attempted_at = @attempted_at;
                """;
            command.Parameters.AddWithValue("@user_key", normalizedUserKey);
            command.Parameters.AddWithValue("@surah_num", attempt.SurahNum);
            command.Parameters.AddWithValue("@ayah_num", attempt.AyahNum);
            command.Parameters.AddWithValue("@rule", (int)violation.Rule);
            command.Parameters.AddWithValue("@attempted_at", attemptedAt.ToString("O"));
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await UpdateProgressFromAttemptAsync(connection, transaction, normalizedUserKey, attempt.SurahNum, attempt.AyahNum, score, attemptedAt, attempt.Mismatches.Count + attempt.TajweedViolations.Count > 0, cancellationToken);
        if (attempt.AssignmentId is not null)
        {
            await using var assignmentCommand = connection.CreateCommand();
            assignmentCommand.Transaction = transaction;
            var assignmentStatus = attempt.MarkAssignmentComplete
                ? (int)LessonAssignmentStatus.Completed
                : (int)LessonAssignmentStatus.InProgress;
            assignmentCommand.CommandText = "UPDATE lesson_assignments SET status = @status, session_id = @session_id WHERE id = @id AND user_key = @user_key;";
            assignmentCommand.Parameters.AddWithValue("@status", assignmentStatus);
            assignmentCommand.Parameters.AddWithValue("@session_id", attempt.SessionId);
            assignmentCommand.Parameters.AddWithValue("@id", attempt.AssignmentId.Value);
            assignmentCommand.Parameters.AddWithValue("@user_key", normalizedUserKey);
            await assignmentCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return new VerseAttempt(attemptId, attempt.SessionId, attempt.AssignmentId, normalizedUserKey, attempt.SurahNum, attempt.AyahNum, score, confidence, attempt.TranscriptionText ?? string.Empty, attemptedAt, attempt.Mismatches, attempt.TajweedViolations);
    }

    public async Task<IReadOnlyList<VerseAttempt>> GetVerseAttemptsAsync(
        string? userKey = null,
        long? sessionId = null,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        await using var connection = CreateOpenConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, session_id, assignment_id, user_key, surah_num, ayah_num, mastery_score,
                   confidence, transcription_text, attempted_at
            FROM verse_attempts
            WHERE user_key = @user_key AND (@session_id IS NULL OR session_id = @session_id)
            ORDER BY attempted_at, id;
            """;
        command.Parameters.AddWithValue("@user_key", ResolveUserKey(userKey));
        command.Parameters.AddWithValue("@session_id", sessionId is null ? DBNull.Value : (object)sessionId.Value);
        var rows = new List<AttemptRow>();
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                rows.Add(new AttemptRow(
                    reader.GetInt64(0), reader.GetInt64(1), reader.IsDBNull(2) ? null : reader.GetInt64(2),
                    reader.GetString(3), reader.GetInt32(4), reader.GetInt32(5), reader.GetDouble(6),
                    reader.GetDouble(7), reader.GetString(8), DateTimeOffset.Parse(reader.GetString(9))));
            }
        }

        var attempts = new List<VerseAttempt>(rows.Count);
        foreach (var row in rows)
        {
            attempts.Add(new VerseAttempt(
                row.Id, row.SessionId, row.AssignmentId, row.UserKey, row.SurahNum, row.AyahNum,
                row.MasteryScore, row.Confidence, row.TranscriptionText, row.AttemptedAt,
                await ReadMismatchesAsync(connection, row.Id, cancellationToken),
                await ReadViolationsAsync(connection, row.Id, cancellationToken)));
        }
        return attempts;
    }

    private sealed record AttemptRow(
        long Id,
        long SessionId,
        long? AssignmentId,
        string UserKey,
        int SurahNum,
        int AyahNum,
        double MasteryScore,
        double Confidence,
        string TranscriptionText,
        DateTimeOffset AttemptedAt);

    private static async Task<LearningPlan?> ReadLearningPlanAsync(SqliteConnection connection, string userKey, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, user_key, daily_new_lesson_target, daily_review_target, created_at, is_active FROM learning_plans WHERE user_key = @user_key LIMIT 1;";
        command.Parameters.AddWithValue("@user_key", userKey);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new LearningPlan(reader.GetInt64(0), reader.GetString(1), reader.GetInt32(2), reader.GetInt32(3), DateTimeOffset.Parse(reader.GetString(4)), reader.GetInt32(5) != 0)
            : null;
    }

    private static LessonAssignment ReadAssignment(SqliteDataReader reader) => new(
        reader.GetInt64(0), reader.GetInt64(1), reader.GetString(2), reader.GetInt32(3), reader.GetInt32(4),
        (LessonAssignmentReason)reader.GetInt32(5), (LessonAssignmentStatus)reader.GetInt32(6),
        DateTimeOffset.Parse(reader.GetString(7)), DateTimeOffset.Parse(reader.GetString(8)), reader.IsDBNull(9) ? null : reader.GetInt64(9));

    private static async Task<RecitationSession?> ReadSessionAsync(SqliteConnection connection, long sessionId, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, plan_id, user_key, started_at, closed_at, status FROM recitation_sessions WHERE id = @id LIMIT 1;";
        command.Parameters.AddWithValue("@id", sessionId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new RecitationSession(reader.GetInt64(0), reader.GetInt64(1), reader.GetString(2), DateTimeOffset.Parse(reader.GetString(3)), reader.IsDBNull(4) ? null : DateTimeOffset.Parse(reader.GetString(4)), (RecitationSessionStatus)reader.GetInt32(5))
            : null;
    }

    private static async Task EnsureSessionOwnershipAsync(SqliteConnection connection, SqliteTransaction transaction, long sessionId, string userKey, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT COUNT(*) FROM recitation_sessions WHERE id = @id AND user_key = @user_key AND status = @status;";
        command.Parameters.AddWithValue("@id", sessionId);
        command.Parameters.AddWithValue("@user_key", userKey);
        command.Parameters.AddWithValue("@status", (int)RecitationSessionStatus.Open);
        if (Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)) == 0)
        {
            throw new InvalidOperationException($"Open recitation session {sessionId} was not found for this user.");
        }
    }

    private async Task UpdateProgressFromAttemptAsync(SqliteConnection connection, SqliteTransaction transaction, string userKey, int surahNum, int ayahNum, double score, DateTimeOffset attemptedAt, bool hasErrors, CancellationToken cancellationToken)
    {
        var existing = await GetProgressMetadataAsync(connection, userKey, surahNum, ayahNum, cancellationToken);
        var attemptCount = existing.AttemptCount + 1;
        var recentErrorCount = hasErrors || score < 0.6 ? existing.RecentErrorCount + 1 : 0;
        var nextReviewAt = new ReviewScheduler().CalculateNextReview(score, recentErrorCount, attemptCount, attemptedAt);
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO memorization_progress (user_key, surah_num, ayah_num, mastery_score, updated_at, next_review_at, attempt_count, recent_error_count)
            VALUES (@user_key, @surah_num, @ayah_num, @score, @updated_at, @next_review_at, @attempt_count, @recent_error_count)
            ON CONFLICT(user_key, surah_num, ayah_num) DO UPDATE SET
                mastery_score = (memorization_progress.mastery_score * @ema_current) + (@score * @ema_new),
                updated_at = @updated_at, next_review_at = @next_review_at,
                attempt_count = @attempt_count, recent_error_count = @recent_error_count;
            """;
        command.Parameters.AddWithValue("@user_key", userKey);
        command.Parameters.AddWithValue("@surah_num", surahNum);
        command.Parameters.AddWithValue("@ayah_num", ayahNum);
        command.Parameters.AddWithValue("@score", score);
        command.Parameters.AddWithValue("@updated_at", attemptedAt.ToString("O"));
        command.Parameters.AddWithValue("@next_review_at", nextReviewAt.ToString("O"));
        command.Parameters.AddWithValue("@attempt_count", attemptCount);
        command.Parameters.AddWithValue("@recent_error_count", recentErrorCount);
        command.Parameters.AddWithValue("@ema_current", EmaCurrentWeight);
        command.Parameters.AddWithValue("@ema_new", EmaNewWeight);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<IReadOnlyList<RecitationWordMismatch>> ReadMismatchesAsync(SqliteConnection connection, long attemptId, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT position, spoken, expected FROM attempt_mismatches WHERE attempt_id = @attempt_id ORDER BY position, id;";
        command.Parameters.AddWithValue("@attempt_id", attemptId);
        var results = new List<RecitationWordMismatch>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new RecitationWordMismatch(reader.GetInt32(0), reader.GetString(1), reader.GetString(2)));
        }
        return results;
    }

    private static async Task<IReadOnlyList<TajweedViolation>> ReadViolationsAsync(SqliteConnection connection, long attemptId, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT word_position, rule, expected_word, spoken_word, hint FROM attempt_tajweed_errors WHERE attempt_id = @attempt_id ORDER BY word_position, id;";
        command.Parameters.AddWithValue("@attempt_id", attemptId);
        var results = new List<TajweedViolation>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new TajweedViolation(reader.GetInt32(0), (TajweedRuleType)reader.GetInt32(1), reader.GetString(2), reader.GetString(3), reader.GetString(4)));
        }
        return results;
    }

    public async Task<IReadOnlyList<TajweedRuleSummary>> GetTajweedRuleSummariesAsync(
        string? userKey = null,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        await using var connection = CreateOpenConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT rule, SUM(error_count) AS total_errors, COUNT(DISTINCT surah_num || '-' || ayah_num) AS affected_verses,
                   MAX(last_attempted_at) AS last_error_at
            FROM tajweed_error_history
            WHERE user_key = @user_key
            GROUP BY rule
            ORDER BY total_errors DESC;
            """;
        command.Parameters.AddWithValue("@user_key", ResolveUserKey(userKey));
        var results = new List<TajweedRuleSummary>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new TajweedRuleSummary(
                (TajweedRuleType)reader.GetInt32(0),
                reader.GetInt32(1),
                reader.GetInt32(2),
                DateTimeOffset.Parse(reader.GetString(3))));
        }
        return results;
    }

    public async Task<IReadOnlyList<TajweedErrorRecord>> GetTajweedErrorsAsync(
        string? userKey = null,
        TajweedRuleType? rule = null,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        await using var connection = CreateOpenConnection();
        await using var command = connection.CreateCommand();
        if (rule is not null)
        {
            command.CommandText = """
                SELECT rule, surah_num, ayah_num, error_count, last_attempted_at
                FROM tajweed_error_history
                WHERE user_key = @user_key AND rule = @rule
                ORDER BY error_count DESC, last_attempted_at DESC
                LIMIT 50;
                """;
            command.Parameters.AddWithValue("@rule", (int)rule.Value);
        }
        else
        {
            command.CommandText = """
                SELECT rule, surah_num, ayah_num, error_count, last_attempted_at
                FROM tajweed_error_history
                WHERE user_key = @user_key
                ORDER BY error_count DESC, last_attempted_at DESC
                LIMIT 100;
                """;
        }
        command.Parameters.AddWithValue("@user_key", ResolveUserKey(userKey));
        var results = new List<TajweedErrorRecord>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new TajweedErrorRecord(
                (TajweedRuleType)reader.GetInt32(0),
                reader.GetInt32(1),
                reader.GetInt32(2),
                reader.GetInt32(3),
                DateTimeOffset.Parse(reader.GetString(4))));
        }
        return results;
    }
}
