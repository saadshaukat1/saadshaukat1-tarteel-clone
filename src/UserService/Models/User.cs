namespace TarteelClone.UserService.Models;

public class User
{
    public int      Id           { get; set; }
    public string   Email        { get; set; } = string.Empty;
    public string   PasswordHash { get; set; } = string.Empty;
    public DateTime CreatedAt    { get; set; } = DateTime.UtcNow;

    public ICollection<RecitationSession>     Sessions  { get; set; } = [];
    public ICollection<MemorizationProgress>  Progress  { get; set; } = [];
}
