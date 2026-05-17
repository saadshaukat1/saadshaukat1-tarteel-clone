namespace TarteelClone.UserService.Models;

public class RecitationSession
{
    public int      Id        { get; set; }
    public int      UserId    { get; set; }
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? EndedAt  { get; set; }

    public User                       User   { get; set; } = null!;
    public ICollection<RecitationError> Errors { get; set; } = [];
}

public class RecitationError
{
    public int      Id        { get; set; }
    public int      SessionId { get; set; }
    public int      VerseId   { get; set; }
    public string   ErrorType { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public RecitationSession Session { get; set; } = null!;
}
