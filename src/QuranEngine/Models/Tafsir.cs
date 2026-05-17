namespace TarteelClone.QuranEngine.Models;

public class Tafsir
{
    public int    Id      { get; set; }
    public int    VerseId { get; set; }
    public string Source  { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;

    public Verse Verse { get; set; } = null!;
}
