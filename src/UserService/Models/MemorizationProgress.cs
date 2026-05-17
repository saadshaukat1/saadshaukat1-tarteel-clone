namespace TarteelClone.UserService.Models;

public class MemorizationProgress
{
    public int    Id           { get; set; }
    public int    UserId       { get; set; }
    public int    SurahNum     { get; set; }
    public int    AyahNum      { get; set; }

    /// <summary>0.0 – 1.0 mastery score computed from recitation history.</summary>
    public double MasteryScore { get; set; }

    public User User { get; set; } = null!;
}
