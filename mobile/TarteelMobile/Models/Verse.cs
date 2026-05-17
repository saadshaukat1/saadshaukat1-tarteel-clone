namespace TarteelMobile.Models;

/// <summary>A single Quran verse loaded from local offline repository data.</summary>
public class Verse
{
    public int    SurahNum    { get; set; }
    public int    AyahNum     { get; set; }
    public string ArabicText  { get; set; } = string.Empty;
    public string Translation { get; set; } = string.Empty;
}
