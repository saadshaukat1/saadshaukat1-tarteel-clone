# Smell Report — Tarteel Desktop

**Date:** 2026-08-07
**Mode:** smell
**Register:** Product (offline Windows desktop utility; MAUI .NET 9)

---

## Catalog of AI Tells

| Tell | Where | Severity |
|---|---|---|
| String-delimited ConverterParameter with hex colors | `App.xaml` + `RecitationPage.xaml` | HIGH |
| Invalid `ThemeBinding` markup extension (does not compile) | `MushafPageView.xaml` | HIGH |
| Border + RoundRectangle as every container | All XAML pages | MEDIUM |
| Picker-as-primary-interaction on desktop | `MushafPageView.xaml` | MEDIUM |
| Text-only TabBar, no icons | `AppShell.xaml` | MEDIUM |
| Algorithmic word-wrap 16-liner, not pre-computed lines | `MushafPageView.xaml.cs` | MEDIUM |
| Auto/\*/Auto header-content-footer grid | `RecitationPage.xaml` | LOW |
| x:DataType on one page only, inconsistent compiled bindings | Mushaf vs Recitation/Login | LOW |

---

## Tell Details & Prescriptions

### 1. String-delimited ConverterParameter with hex colors — HIGH

```xml
<!-- RecitationPage.xaml line 170-171 -->
Stroke="{Binding IsVerseCorrect,
                Converter={StaticResource BoolToColorConverter},
                ConverterParameter='#2D8C4F|#C04040'}"
BackgroundColor="{Binding IsVerseCorrect,
                    Converter={StaticResource BoolToColorConverter},
                    ConverterParameter='#EBF5EE|#FDF0ED'}"
```

The `BoolToColorConverter` splits a `|`-delimited string of hex colors. This is the "I need to branch on a boolean but don't want to touch the ViewModel" tell — pushing color decisions into the View layer via a string parameter. The ViewModel already knows the semantic state (`IsVerseCorrect`). Colors should be bound to named token resources, not embedded as literals in converter parameters.

**Prescription:** Replace with VisualStateManager or direct binding to named color resources. Move `bool → color` logic to the ViewModel (e.g., `VerseCorrectColor` property bound to `{StaticResource Success}` or `{StaticResource Error}`).

### 2. Invalid `ThemeBinding` markup extension — HIGH

```xml
<!-- MushafPageView.xaml line 11, 58-59, 76-77, 85-86, 94-95, 100 -->
BackgroundColor="{ThemeBinding Primary, Light={StaticResource Surface}, Dark={StaticResource SurfaceDark}}"
```

`ThemeBinding` is not a valid MAUI markup extension. The build fails:
```
XamlC error XC0000: Cannot resolve type "http://schemas.microsoft.com/dotnet/2021/maui:ThemeBinding"
```

This is the "hallucinated API" tell — generating XAML that looks plausible but has never been validated against the compiler. The developer wrote `ThemeBinding` thinking it was a real extension, then the file was committed without building.

**Prescription:** Use `{AppThemeBinding Light=..., Dark=...}` — the actual MAUI extension. Or, since dark mode isn't implemented yet, remove all theme bindings and use light-only resources until `/design recolor` adds the dark theme.

### 3. Border + RoundRectangle as every container — MEDIUM

Every panel in every XAML file uses:
```xml
<Border StrokeShape="RoundRectangle 12" ...>
```

Model alerts, verse card, transcription block, tajweed panel, debug panel, mushroom parchment — all Borders with `RoundRectangle` corners. This is the "I need a container, which primitive?" tell. There's no visual hierarchy between surface layers: a secondary diagnostic panel and a primary content card have the same stroke width, same corner radius, same treatment.

**Prescription:** Establish a corner-radius scale: 16 for primary cards, 8 for secondary panels, 0 for flat surfaces. Use different StrokeThickness (primary: 1, secondary: 0) to distinguish depth layers. Let the Mushaf page's parchment panel be the only fully-decorated surface — everything else should flatten.

### 4. Picker-as-primary-interaction on desktop — MEDIUM

```xml
<!-- MushafPageView.xaml lines 52-87 -->
<Picker Title="Juz" ... />
<Picker Title="Surah" ... />
<Picker Title="Āyah" ... />
```

Native MAUI Pickers render as generic WinUI dropdowns. On a Windows desktop app, this is the "throw a native control at it" tell — avoiding the hard work of designing a proper selector by deferring to platform defaults. The Pickers have thin padding, no search, no visual character matching the rest of the app.

**Prescription:** Either commit to native Pickers with proper styling (match border color, placeholder treatment) or replace with a custom flyout/menu button that opens a styled grid/list. The `JumpToJuzCommand` and `JumpToVerseCommand` already exist — wire them to a custom selector.

### 5. Text-only TabBar — MEDIUM

```xml
<!-- AppShell.xaml lines 8-17 -->
<ShellContent Title="Recite" ... />
<ShellContent Title="Mushaf" ... />
<ShellContent Title="Progress" ... />
```

Three tabs, no icons. The "I can't choose good icons so I'll use text" tell. This is especially egregious for a Quran app where tab icons carry semantic meaning (a book for Mushaf, a mic for Recite, a chart for Progress).

**Prescription:** Add icon fonts or SVG tab icons. MAUI Shell supports `Icon` attributes.

### 6. Algorithmic word-wrap 16-liner — MEDIUM

```csharp
// MushafPageView.xaml.cs line 118-227, Build16Lines()
var targetCharsPerLine = Math.Max((double)totalChars / RowsPerPage, 1);
// ...balanced distribution by character count...
```

The 16-line Mushaf page is generated by packing words by estimated character count per line. A real Madani Mushaf has pre-determined line breaks — each of the 16 lines is a specific textual unit, not an algorithmic artifact. The word-wrap approach produces lines that vary in actual rendered width (Arabic glyphs are not monospaced) and loses the authentic Mushaf rhythm.

**Prescription:** Use pre-computed line data from the mushaf page map. If line-level data isn't available, render verses in natural flow within the 16-row grid, letting each verse occupy its natural space rather than forcing a word-wrap approximation.

### 7. Auto/\*/Auto header-content-footer grid — LOW

```xml
<!-- RecitationPage.xaml line 8 -->
<Grid RowDefinitions="Auto,*,Auto" ...>
```

The header-content-footer grid is the default "I need to structure a page" pattern. It's functional but generic. On a desktop app where the content area is the verse card, the header contains 4 stacked toolbar elements and the footer contains a 4-column button grid — the visual rhythm is driven by convenience of the grid definition, not by content priority.

**Prescription:** Use explicit row heights for the footer (the action bar is a fixed-height element) and consider a thinner header that collapses on scroll.

### 8. Inconsistent x:DataType adoption — LOW

```xml
<!-- MushafPageView.xaml line 7 -->
x:DataType="vm:MushafPageViewModel"
```

`MushafPageView` has `x:DataType` (compiled bindings). `RecitationPage`, `LoginPage`, and `ProgressPage` do not. If you're paying the cost of adding compiled bindings to one page, the other pages should follow. Half-adopted is the "I started something and forgot" tell.

**Prescription:** Add `x:DataType` to all pages and DataTemplates consistently, or remove it from MushafPageView until the rest catch up.
