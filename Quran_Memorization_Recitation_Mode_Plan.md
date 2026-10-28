# **Quran Memorization Recitation Mode (.NET MAUI Adaptation)**

# **Implementation & Reverse-Engineering Technical Guide**

---

# **1\. Executive Summary & Interaction Analysis**

This specification details the architecture, reverse-engineering, and complete implementation of the voice-activated Blind Quran Recitation Mode (Hifz Testing Canvas) for .NET MAUI. The system allows a reciter to recite from memory onto a visually blank Mushaf page. As words are spoken, they are recognized in real time, aligned against canonical Uthmani Quranic text using fuzzy matching, and progressively revealed at fixed typographic coordinates without causing page layout reflow.+-----------------------------------------------------------------------+

|                       RECITATION PIPELINE                             |

|                                                                       |

|  \[ Audio In \] \---\> \[ ASR Engine \] \---\> \[ Arabic Normalizer \]         |

|                                                 |                     |

|                                                 v                     |

|  \[ Canvas \] \<--- \[ Canvas Drawable \] \<--- \[ Alignment Engine \]        |

|  (Revealed)        (Redraw Event)        (State & Gap Tracking)       |

\+-----------------------------------------------------------------------+

## **Key Interaction Characteristics Observed:**

* **Zero-Layout-Shift Canvas:** All verse numbers and glyph locations are pinned prior to recitation. The page layout is mathematically fixed using page coordinate metadata.  
* **Live Feedback Pill:** A floating pill adjacent to the main action button displays the immediate token recognized by the ASR engine before it locks into the page layout.  
* **Mistake and Gap Detection:** When a word is skipped (e.g., omitting "أَيُّكُمْ"), the system detects the forward gap, marks skipped tokens, increments the mistake counter, and updates accuracy percentage.  
* **Self-Healing Backtracking:** When the reciter self-corrects and repeats the omitted phrase, the engine detects retroactive matches, updates the missing word state to corrected, and restores continuity.  
* **Session Control & Heads-Up Display (HUD):** Dynamic tracking of session mistakes, mistakes-per-page, overall percentage accuracy, elapsed time, and dynamic FAB toggles.

---

# **2\. Visual Interaction Wireframes**

## **State 0: Initial Idle / Blank Canvas**

The canvas displays the Surah header and Bismillah. Word text remains hidden while circular verse-end ornaments (*Aya markers*) remain pinned in their pre-calculated absolute positions.+-------------------------------------------------------------+

| \[=\] Al-Mulk (Page 562 | Juz 29 | Hzb 57\)       \[Bookmark\] \[\*\] |

\+-------------------------------------------------------------+

| /=========================================================\\ |

| |                    سُورَةُ الْمُلْكِ                     | |

| \\=========================================================/ |

|                                                             |

|                بِسْمِ اللَّهِ الرَّحْمَٰنِ الرَّحِيمِ               |

|                                                             |

|         ( 1 )                               ( 2 )           |

|   ( 3 )                                                     |

|                     ( 4 )                   ( 5 )           |

|         ( 6 )                               ( 7 )           |

|                                                             |

\+-------------------------------------------------------------+

| \[0\] \[0/pg\] \[100%\] \[00:00\] \[Stop\]               \[ (Microphone) \] |

\+-------------------------------------------------------------+

## **State 1: Speech Recognition Active and Word Reveal**

Tapping the FAB starts the audio stream. Words are matched and revealed progressively from right to left. A live word pill appears next to the floating action button.+-------------------------------------------------------------+

|                بِسْمِ اللَّهِ الرَّحْمَٰنِ الرَّحِيمِ               |

|                                                             |

|   تَبَارَكَ الَّذِي بِيَدِهِ الْمُلْكُ وَهُوَ عَلَى كُلِّ شَيْءٍ قَدِيرٌ (١)   |

|                                                             |

|         ( 2 )                               ( 3 )           |

\+-------------------------------------------------------------+

| \[0\] \[0/pg\] \[100%\] \[00:50\] \[Stop\]    \[قَدِيرٌ\]   \[ (Pause ||) \] |

\+-------------------------------------------------------------+

## **State 2: Skipped Word Detection**

The reciter skips "أَيُّكُمْ" in Ayah 2\. The matcher detects a forward jump, highlights the gap, increments mistakes to 1, and recalculates accuracy.+-------------------------------------------------------------+

|   تَبَارَكَ الَّذِي بِيَدِهِ الْمُلْكُ وَهُوَ عَلَى كُلِّ شَيْءٍ قَدِيرٌ (١)   |

|   الَّذِي خَلَقَ \[\!\] الْمَوْتَ وَالْحَيَاةَ لِيَبْلُوَكُمْ أَحْسَنُ عَمَلًا    |

\+-------------------------------------------------------------+

| \[\! 1\] \[1/pg\] \[99.2%\] \[00:58\] \[Stop\]           \[ (Pause ||) \] |

\+-------------------------------------------------------------+

## **State 3: Backtrack Self-Correction**

The reciter re-reads "لِيَبْلُوَكُمْ أَيُّكُمْ أَحْسَنُ عَمَلًا". The engine detects the backwards match, inserts the missing word "أَيُّكُمْ" in green, and updates status flags.+-------------------------------------------------------------+

|   تَبَارَكَ الَّذِي بِيَدِهِ الْمُلْكُ وَهُوَ عَلَى كُلِّ شَيْءٍ قَدِيرٌ (١)   |

|   الَّذِي خَلَقَ الْمَوْتَ وَالْحَيَاةَ لِيَبْلُوَكُمْ \[أَيُّكُمْ\] أَحْسَنُ     |

|   عَمَلًا وَهُوَ الْعَزِيزُ الْغَفُورُ (٢)                      |

\+-------------------------------------------------------------+

| \[\! 1\] \[1/pg\] \[99.2%\] \[01:05\] \[Stop\]  \[الْغَفُورُ\] \[ (Pause ||) \] |

\+-------------------------------------------------------------+

---

# **3\. Architecture & Data Models**

## **3.1 Layout Shift Mitigation Strategy**

Standard MAUI controls (`FlexLayout`, `Label`) undergo layout passes upon text mutation, causing reflows that disrupt the 15-line Madinah Mushaf structure.+-----------------------------------------------------------------+

|                       CANVAS RENDER METHOD                      |

|                                                                 |

|  1\. Draw Background Bounds                                       |

|  2\. Draw Fixed Ayah Markers (Absolute Vector Coordinates)       |

|  3\. Iterate Words:                                              |

|     ├─ Status \== Hidden    \--\> Skip Draw                        |

|     ├─ Status \== Revealed  \--\> Draw Text (\#F1F5F9)               |

|     ├─ Status \== Skipped   \--\> Draw Accent / Indicator          |

|     └─ Status \== Corrected \--\> Draw Text (\#4ADE80)              |

\+-----------------------------------------------------------------+

## **3.2 Data Models (`Models.cs`)**

namespace QuranApp.Models;

public enum WordRecitationStatus

{

    Hidden \= 0,

    Revealed \= 1,

    Skipped \= 2,

    Corrected \= 3,

    Active \= 4

}

public class QuranWordModel

{

    public string Id { get; set; } \= string.Empty; // e.g., "67:1:1"

    public int SurahNumber { get; set; }

    public int AyahNumber { get; set; }

    public int WordIndex { get; set; }

    public string UthmaniText { get; set; } \= string.Empty;

    public string NormalizedText { get; set; } \= string.Empty;

    public int LineNumber { get; set; }

    public float X { get; set; }

    public float Y { get; set; }

    public float Width { get; set; }

    public WordRecitationStatus Status { get; set; } \= WordRecitationStatus.Hidden;

}

public class AyahMarkerModel

{

    public int AyahNumber { get; set; }

    public float X { get; set; }

    public float Y { get; set; }

    public float Radius { get; set; } \= 14f;

}

---

# **4\. Normalization & Alignment Engine**

## **4.1 Arabic Normalization Utility**

namespace QuranApp.Services;

using System.Text.RegularExpressions;

public static class ArabicTextNormalizer

{

    private static readonly Regex HarakatRegex \= new Regex(@"\[ً-ٰٟ\]", RegexOptions.Compiled);

    public static string Normalize(string input)

    {

        if (string.IsNullOrWhiteSpace(input)) return string.Empty;

        // Strip tashkeel / harakat

        string normalized \= HarakatRegex.Replace(input, string.Empty);

        // Normalize Alifs, Taa Marbuta, and Yaa

        normalized \= Regex.Replace(normalized, @"\[إأآ\]", "ا");

        normalized \= normalized.Replace("ة", "ه");

        normalized \= normalized.Replace("ى", "ي");

        return normalized.Trim();

    }

}

## **4.2 Alignment Engine with Levenshtein Distance & Sliding Window**

namespace QuranApp.Services;

public enum AlignmentOutcome

{

    ExactMatch,

    ForwardGap,

    BacktrackCorrection,

    NoMatch

}

public class AlignmentResult

{

    public AlignmentOutcome Outcome { get; set; }

    public int MatchedIndex { get; set; }

    public List\<int\> SkippedIndices { get; set; } \= new();

}

public class QuranAlignmentEngine

{

    public static int ComputeLevenshtein(string s, string t)

    {

        if (string.IsNullOrEmpty(s)) return t?.Length ?? 0;

        if (string.IsNullOrEmpty(t)) return s.Length;

        int n \= s.Length;

        int m \= t.Length;

        int\[,\] d \= new int\[n \+ 1, m \+ 1\];

        for (int i \= 0; i \<= n; d\[i, 0\] \= i++) ;

        for (int j \= 0; j \<= m; d\[0, j\] \= j++) ;

        for (int i \= 1; i \<= n; i++)

        {

            for (int j \= 1; j \<= m; j++)

            {

                int cost \= (t\[j \- 1\] \== s\[i \- 1\]) ? 0 : 1;

                d\[i, j\] \= Math.Min(

                    Math.Min(d\[i \- 1, j\] \+ 1, d\[i, j \- 1\] \+ 1),

                    d\[i \- 1, j \- 1\] \+ cost);

            }

        }

        return d\[n, m\];

    }

    public AlignmentResult AlignToken(

        string recognizedToken, 

        IList\<QuranWordModel\> words, 

        int currentIndex, 

        int lookaheadWindow \= 4\)

    {

        string norm \= ArabicTextNormalizer.Normalize(recognizedToken);

        var result \= new AlignmentResult { Outcome \= AlignmentOutcome.NoMatch, MatchedIndex \= currentIndex };

        if (string.IsNullOrEmpty(norm) || currentIndex \>= words.Count) return result;

        // 1\. Direct Next Word Check

        if (ComputeLevenshtein(norm, words\[currentIndex\].NormalizedText) \<= 1\)

        {

            result.Outcome \= AlignmentOutcome.ExactMatch;

            result.MatchedIndex \= currentIndex;

            return result;

        }

        // 2\. Forward Lookahead (Gap / Skipped Word)

        int maxLookahead \= Math.Min(currentIndex \+ lookaheadWindow, words.Count);

        for (int i \= currentIndex \+ 1; i \< maxLookahead; i++)

        {

            if (ComputeLevenshtein(norm, words\[i\].NormalizedText) \<= 1\)

            {

                result.Outcome \= AlignmentOutcome.ForwardGap;

                result.MatchedIndex \= i;

                for (int s \= currentIndex; s \< i; s++)

                {

                    result.SkippedIndices.Add(s);

                }

                return result;

            }

        }

        // 3\. Backtrack Check (Self-Correction)

        int minLookback \= Math.Max(0, currentIndex \- 4);

        for (int b \= currentIndex \- 1; b \>= minLookback; b--)

        {

            if (ComputeLevenshtein(norm, words\[b\].NormalizedText) \<= 1\)

            {

                result.Outcome \= AlignmentOutcome.BacktrackCorrection;

                result.MatchedIndex \= b;

                return result;

            }

        }

        return result;

    }

}

---

# **5\. Speech Service Integration Interface**

namespace QuranApp.Services;

public interface ISpeechRecognitionService

{

    event EventHandler\<string\> WordRecognized;

    Task\<bool\> RequestPermissionsAsync();

    Task StartListeningAsync(string languageCode \= "ar-SA");

    Task StopListeningAsync();

    bool IsListening { get; }

}

---

# **6\. GraphicsView Canvas Implementation**

namespace QuranApp.Views;

using QuranApp.Models;

public class MushafCanvasDrawable : IDrawable

{

    public IList\<QuranWordModel\> Words { get; set; } \= new List\<QuranWordModel\>();

    public IList\<AyahMarkerModel\> Markers { get; set; } \= new List\<AyahMarkerModel\>();

    public void Draw(ICanvas canvas, RectF dirtyRect)

    {

        canvas.FillColor \= Color.FromArgb("\#111214");

        canvas.FillRectangle(dirtyRect);

        // Render Fixed Verse Ornaments

        foreach (var marker in Markers)

        {

            canvas.StrokeColor \= Color.FromArgb("\#525866");

            canvas.StrokeSize \= 1.5f;

            canvas.DrawCircle(marker.X, marker.Y, marker.Radius);

            canvas.FontColor \= Color.FromArgb("\#8E95A3");

            canvas.FontSize \= 11;

            canvas.DrawString(

                marker.AyahNumber.ToString(), 

                marker.X \- 10, marker.Y \- 8, 20, 16, 

                HorizontalAlignment.Center, VerticalAlignment.Center);

        }

        // Render Words Based on Recitation Status

        foreach (var word in Words)

        {

            if (word.Status \== WordRecitationStatus.Hidden)

                continue;

            Color textColor \= word.Status switch

            {

                WordRecitationStatus.Revealed \=\> Color.FromArgb("\#F1F5F9"),

                WordRecitationStatus.Corrected \=\> Color.FromArgb("\#4ADE80"),

                WordRecitationStatus.Skipped \=\> Color.FromArgb("\#38BDF8"),

                \_ \=\> Color.FromArgb("\#CBD5E1")

            };

            canvas.FontColor \= textColor;

            canvas.FontSize \= 22;

            canvas.DrawString(

                word.UthmaniText, 

                word.X, word.Y, word.Width, 36, 

                HorizontalAlignment.Right, VerticalAlignment.Center);

        }

    }

}

---

# **7\. Presentation Layer & State Management**

## **7.1 XAML Interface Definition (`RecitationPage.xaml`)**

\<ContentPage xmlns="http\://schemas.microsoft.com/dotnet/2021/maui"

             xmlns:x="http\://schemas.microsoft.com/winfx/2009/xaml"

             xmlns:vm="clr-namespace:QuranApp.ViewModels"

             xmlns:views="clr-namespace:QuranApp.Views"

             x:Class="QuranApp.Views.RecitationPage"

             BackgroundColor="\#111214"

             NavigationPage.HasNavigationBar="False"\>

    \<Grid RowDefinitions="Auto, Auto, \*, Auto"\>

        \<\!-- Header Controls \--\>

        \<Grid Grid.Row="0" ColumnDefinitions="Auto, \*, Auto" Padding="16, 12" BackgroundColor="\#16181B"\>

            \<HorizontalStackLayout Spacing="10" VerticalOptions="Center"\>

                \<ImageButton Source="icon\_menu.png" WidthRequest="24" HeightRequest="24"/\>

                \<VerticalStackLayout\>

                    \<Label Text="Al-Mulk" TextColor="\#F1F5F9" FontAttributes="Bold" FontSize="16"/\>

                    \<Label Text="Page 562 | Juz 29 | Hzb 57" TextColor="\#94A3B8" FontSize="11"/\>

                \</VerticalStackLayout\>

            \</HorizontalStackLayout\>

            \<HorizontalStackLayout Grid.Column="2" Spacing="16" VerticalOptions="Center"\>

                \<ImageButton Source="icon\_bookmark.png" WidthRequest="20" HeightRequest="20"/\>

                \<ImageButton Source="icon\_search.png" WidthRequest="20" HeightRequest="20"/\>

                \<ImageButton Source="icon\_settings.png" WidthRequest="20" HeightRequest="20"/\>

            \</HorizontalStackLayout\>

        \</Grid\>

        \<\!-- Surah Meta Header \--\>

        \<VerticalStackLayout Grid.Row="1" Padding="16, 8" Spacing="8"\>

            \<Border Stroke="\#334155" StrokeThickness="1" BackgroundColor="\#181B1F" Padding="12, 6" StrokeShape="RoundRectangle 8"\>

                \<Label Text="سُورَةُ الْمُلْكِ" HorizontalOptions="Center" TextColor="\#F8FAFC" FontSize="20" FontFamily="AmiriQuran"/\>

            \</Border\>

            \<Label Text="بِسْمِ اللَّهِ الرَّحْمَٰنِ الرَّحِيمِ" HorizontalOptions="Center" TextColor="\#94A3B8" FontSize="18" FontFamily="AmiriQuran"/\>

        \</VerticalStackLayout\>

        \<\!-- Zero-Shift Drawing Canvas \--\>

        \<GraphicsView Grid.Row="2" x:Name="MushafView" Margin="12, 4"/\>

        \<\!-- Real-Time Recitation HUD \--\>

        \<Grid Grid.Row="3" BackgroundColor="\#16181B" Padding="16, 10" ColumnDefinitions="Auto, \*, Auto"\>

            

            \<HorizontalStackLayout Spacing="8" VerticalOptions="Center"\>

                \<Border BackgroundColor="\#DC2626" StrokeShape="RoundRectangle 12" Padding="8, 2"\>

                    \<Label Text="{Binding MistakesCount, StringFormat='👤 {0}'}" TextColor="White" FontSize="12" FontAttributes="Bold"/\>

                \</Border\>

                \<Label Text="{Binding MistakesCount, StringFormat='{0}/pg'}" TextColor="\#94A3B8" VerticalOptions="Center" FontSize="12"/\>

                \<Border BackgroundColor="\#22252A" StrokeShape="RoundRectangle 6" Padding="6, 2"\>

                    \<Label Text="{Binding Accuracy, StringFormat='{0:F1}%'}" TextColor="\#38BDF8" FontSize="12" FontAttributes="Bold"/\>

                \</Border\>

                \<Label Text="{Binding ElapsedTimeString, StringFormat='⏱ {0}'}" TextColor="\#E2E8F0" VerticalOptions="Center" FontSize="12"/\>

                \<Button Text="■" Command="{Binding StopRecitationCommand}" BackgroundColor="\#7F1D1D" TextColor="\#EF4444" HeightRequest="28" WidthRequest="28" Padding="0" CornerRadius="4"/\>

            \</HorizontalStackLayout\>

            \<\!-- Recognized Word Pill \--\>

            \<Border Grid.Column="1" HorizontalOptions="End" Margin="0,0,10,0"

                    IsVisible="{Binding HasLiveToken}"

                    BackgroundColor="\#1F2937" Stroke="\#374151" StrokeShape="RoundRectangle 16" Padding="12, 4"\>

                \<Label Text="{Binding LiveTokenText}" TextColor="\#4ADE80" FontSize="14" FontFamily="AmiriQuran"/\>

            \</Border\>

            \<\!-- State Action Button \--\>

            \<Button Grid.Column="2"

                    Command="{Binding ToggleListeningCommand}"

                    Text="{Binding FabIconText}"

                    BackgroundColor="\#22C55E"

                    TextColor="Black"

                    FontAttributes="Bold"

                    FontSize="20"

                    HeightRequest="56"

                    WidthRequest="56"

                    CornerRadius="28"/\>

        \</Grid\>

    \</Grid\>

\</ContentPage\>

## **7.2 ViewModel (`RecitationViewModel.cs`)**

namespace QuranApp.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;

using CommunityToolkit.Mvvm.Input;

using QuranApp.Models;

using QuranApp.Services;

using System.Diagnostics;

public partial class RecitationViewModel : ObservableObject

{

    private readonly ISpeechRecognitionService \_speechService;

    private readonly QuranAlignmentEngine \_alignmentEngine \= new();

    private readonly Stopwatch \_stopwatch \= new();

    private readonly IDispatcherTimer \_timer;

    \[ObservableProperty\] private int \_mistakesCount \= 0;

    \[ObservableProperty\] private double \_accuracy \= 100.0;

    \[ObservableProperty\] private string \_elapsedTimeString \= "00:00";

    \[ObservableProperty\] private string \_liveTokenText \= string.Empty;

    \[ObservableProperty\] private bool \_hasLiveToken \= false;

    \[ObservableProperty\] private bool \_isListening \= false;

    \[ObservableProperty\] private string \_fabIconText \= "🎙";

    public List\<QuranWordModel\> PageWords { get; set; } \= new();

    public int CurrentWordCursor { get; set; } \= 0;

    public event Action? RequestCanvasRedraw;

    public RecitationViewModel(ISpeechRecognitionService speechService)

    {

        \_speechService \= speechService;

        \_speechService.WordRecognized \+= OnWordRecognized;

        \_timer \= Application.Current.Dispatcher.CreateTimer();

        \_timer.Interval \= TimeSpan.FromSeconds(1);

        \_timer.Tick \+= (s, e) \=\> ElapsedTimeString \= \_stopwatch.Elapsed.ToString(@"mm\\:ss");

    }

    \[RelayCommand\]

    private async Task ToggleListeningAsync()

    {

        if (IsListening)

        {

            await \_speechService.StopListeningAsync();

            \_stopwatch.Stop();

            \_timer.Stop();

            IsListening \= false;

            FabIconText \= "🎙";

            HasLiveToken \= false;

        }

        else

        {

            bool granted \= await \_speechService.RequestPermissionsAsync();

            if (\!granted) return;

            await \_speechService.StartListeningAsync("ar-SA");

            \_stopwatch.Start();

            \_timer.Start();

            IsListening \= true;

            FabIconText \= "⏸";

        }

    }

    \[RelayCommand\]

    private void StopRecitation()

    {

        \_ \= \_speechService.StopListeningAsync();

        \_stopwatch.Reset();

        \_timer.Stop();

        IsListening \= false;

        FabIconText \= "🎙";

        HasLiveToken \= false;

        ElapsedTimeString \= "00:00";

    }

    private void OnWordRecognized(object? sender, string token)

    {

        MainThread.BeginInvokeOnMainThread(() \=\>

        {

            LiveTokenText \= token;

            HasLiveToken \= true;

            var match \= \_alignmentEngine.AlignToken(token, PageWords, CurrentWordCursor);

            switch (match.Outcome)

            {

                case AlignmentOutcome.ExactMatch:

                    PageWords\[match.MatchedIndex\].Status \= WordRecitationStatus.Revealed;

                    CurrentWordCursor \= match.MatchedIndex \+ 1;

                    break;

                case AlignmentOutcome.ForwardGap:

                    foreach (var skippedIdx in match.SkippedIndices)

                    {

                        PageWords\[skippedIdx\].Status \= WordRecitationStatus.Skipped;

                        MistakesCount++;

                    }

                    PageWords\[match.MatchedIndex\].Status \= WordRecitationStatus.Revealed;

                    CurrentWordCursor \= match.MatchedIndex \+ 1;

                    RecalculateAccuracy();

                    break;

                case AlignmentOutcome.BacktrackCorrection:

                    PageWords\[match.MatchedIndex\].Status \= WordRecitationStatus.Corrected;

                    break;

            }

            RequestCanvasRedraw?.Invoke();

        });

    }

    private void RecalculateAccuracy()

    {

        int totalExpected \= Math.Max(1, CurrentWordCursor);

        Accuracy \= Math.Max(0.0, ((totalExpected \- MistakesCount) / (double)totalExpected) \* 100.0);

    }

}

---

# **8\. Implementation Milestones**

| Task | Phase Description | Key Deliverables |
| :---- | :---- | :---- |
| **Phase 1** | Uthmani Typography Setup | Import `AmiriQuran.ttf`, register fonts, and create JSON structural model for Page 562\. |
| **Phase 2** | Canvas Graphics Engine | Build `MushafCanvasDrawable`, link `Invalidate()` calls, and verify static layout binding. |
| **Phase 3** | Speech Recognition Integration | Implement `ISpeechRecognitionService`, setup OS microphone permissions, and handle streams. |
| **Phase 4** | Fuzzy Engine & Backtracking | Configure text normalizer, build Levenshtein metric, and validate self-healing logic. |
| **Phase 5** | HUD Controls & Verification | Bind stats bar, implement floating live-token view, and test full end-to-end flow. |

