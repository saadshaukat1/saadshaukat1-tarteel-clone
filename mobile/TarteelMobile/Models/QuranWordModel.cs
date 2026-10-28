namespace TarteelMobile.Models;

public enum WordRecitationStatus
{
    Hidden = 0,
    Revealed = 1,
    Skipped = 2,
    Corrected = 3,
    Active = 4
}

public class QuranWordModel
{
    public string Id { get; set; } = string.Empty; // e.g. "67:1:1"
    public int SurahNumber { get; set; }
    public int AyahNumber { get; set; }
    public int WordIndex { get; set; }
    public string UthmaniText { get; set; } = string.Empty;
    public string NormalizedText { get; set; } = string.Empty;
    public int LineNumber { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
    public float Width { get; set; }
    public float Height { get; set; } = 36f;
    public WordRecitationStatus Status { get; set; } = WordRecitationStatus.Hidden;
}

public class AyahMarkerModel
{
    public int AyahNumber { get; set; }
    public int LineNumber { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
    public float Radius { get; set; } = 14f;
}
