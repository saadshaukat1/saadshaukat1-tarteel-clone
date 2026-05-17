namespace TarteelMobile.Models;

/// <summary>A single Quran verse as returned by the API.</summary>
public class Verse
{
    public int    SurahNum    { get; set; }
    public int    AyahNum     { get; set; }
    public string ArabicText  { get; set; } = string.Empty;
    public string Translation { get; set; } = string.Empty;
}
