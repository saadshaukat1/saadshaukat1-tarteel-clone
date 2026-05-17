namespace TarteelClone.QuranEngine.Models;

public class Verse
{
    public int    Id          { get; set; }
    public int    SurahNum    { get; set; }
    public int    AyahNum     { get; set; }

    /// <summary>Uthmani script (canonical Arabic).</summary>
    public string ArabicText  { get; set; } = string.Empty;

    /// <summary>Simple text for lightweight matching.</summary>
    public string UthmaniText { get; set; } = string.Empty;

    public ICollection<Translation> Translations { get; set; } = [];
    public ICollection<Tafsir>      Tafsirs       { get; set; } = [];
}
