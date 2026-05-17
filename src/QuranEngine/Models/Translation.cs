namespace TarteelClone.QuranEngine.Models;

public class Translation
{
    public int    Id         { get; set; }
    public int    VerseId    { get; set; }
    public string Language   { get; set; } = string.Empty;
    public string Text       { get; set; } = string.Empty;
    public string Translator { get; set; } = string.Empty;

    public Verse Verse { get; set; } = null!;
}
