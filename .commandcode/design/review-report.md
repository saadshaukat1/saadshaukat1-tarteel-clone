# Design Review — Tarteel Desktop

**Date:** 2026-08-07
**Score:** 38/50
**Register:** Product (offline Windows desktop utility; MAUI .NET 9)
**Prior reviews:** June 10 (24/50), July 15 checkup (45/60)

---

## First Impression — 8/10

The app has graduated from "competent utility" to authored product. The warm parchment surface (#F9F6EF), deep green primary (#1A6B3C), and gold accent (#C9A84C) create a distinct Islamic-app palette that reads as reverent without being gaudy. The login page wordmark with gold accent rules is a genuine brand moment — the emoji logo is long gone. The 80×80 mic button at bottom-center with the persistent confidence bar is the signature interaction pattern.

The app knows what it is: a desktop recitation coach. Not a web app ported to Windows, not a mobile-first design stretched to fill a monitor. The 16-liner Mushaf page with its 720px centered parchment panel and auto-advance scroll is the strongest visual surface on screen.

**What keeps this from 9-10:** No dark mode. Every user on a dark OS theme gets a cream flood that feels like an uninvited light switch. The app identity is still a text wordmark — no icon, no mark, no visual shorthand for the taskbar or Start menu.

---

## Hierarchy — 7/10

The 3-row grid (toolbar / hero / controls) on the Recitation page is a correct work pattern for a Monitor surface: the operator needs persistent status, a dominant focal object, and always-accessible controls. The mic button at 80×80 is the right visual weight for the primary action.

The Mushaf page has clean hierarchy: page navigation across the top, the 16-row grid as the hero, a one-line scroll hint at the bottom. This is the most resolved composition in the app.

The Progress page is a straightforward list with an EMA summary header — adequate, not ambitious.

**What drags the score down:**

- The scrollable middle area on Recitation still hosts 5 conditionally visible bordered panels (verse card, hidden-verse placeholder, transcription block, tajweed corrections, ASR debug). They stack vertically with equal visual weight. When multiple panels are visible simultaneously, the eye doesn't know which surface is the current "truth."
- The Surah/Ayah picker row is cramped: three controls in a single Grid with 6px column spacing. The 60px "Go" button and 64px Ayah picker compete for breathing room.
- The transcription section lives inside the verse card but is separated by a thin BoxView divider — the visual relationship between Arabic verse and user transcription is unclear. Are they equal? Is one subordinate?

---

## Color Voice — 8/10

The token system is real and enforced. 51 named Color resources in App.xaml. No hex code spam in page XAML — the remaining inline hex values are exclusively in ConverterParameter strings (required by BoolToColorConverter). This is the earliest review complaint, and it's been fully resolved.

The palette has intent: warm cream surface, deep green primary with hover/pressed variants, gold accent that's actually used (LoginPage accent rules, Focus/FocusRing tokens), and full state color families for success (green), error (red), warning (amber), and info (green — see below).

State color coverage is complete. Success has bg/border/text/light. Error has bg/border/text/light plus SurfaceErrorBg. Warning has bg/border/text/light plus a secondary text. Info exists but is underdeveloped (only bg/border/text, and it reuses green instead of a distinct blue).

**What keeps this from 9-10:**

- **No dark mode.** SurfaceDark (#F5F1E8), SurfacePanelDark (#EFEAD8), and PrimaryDark (#0F3D22) tokens exist but nothing applies them. No AppThemeBinding anywhere. A dark OS theme user gets no adaptation. This is P1.
- **Info palette reuses Success green.** Info (#2D8C4F), InfoBg (#EBF5EE), InfoBorder (#A3D4B3), InfoText (#1F6339) are identical to their Success counterparts. Info should be a distinct blue or teal so the user can distinguish "something succeeded" from "here is neutral information."
- **Confidence bar is green/red only.** The progress bar color bindings toggle between #2D8C4F (matched) and #C04040 (error). Users with deuteranopia or protanopia will see these as the same muddy brown. Add a shape or pattern distinction — or switch to blue/green for the binary state.
- **Debug panel (#1A1D21, #C8E6C9) has no dark-mode counterpart.** When the app gets dark mode, the debug panel will blend into a dark surface instead of standing out as the "terminal view."

---

## Type Voice — 8/10

This is the biggest turnaround from the June review (3/10 → 8/10). The app now has a real type system:

- **Arabic typography is correct.** NotoNaskhArabic is registered via csproj `<MauiFont>`, applied as ArabicFontFamily with 28px hero size, 1.8 line-height, 700px max-width measure, RTL flow direction. Arabic text reads naturally — letters join, diacritics position correctly, and the measure prevents runaway lines on wide monitors.
- **Tokenized type scale.** Nano (10px) through HeroVerse (36px) — 12 named FontSize resources with consistent naming.
- **Line heights are tokenized.** Tight (1.1), Normal (1.4), Relaxed (1.6), Arabic (1.8).
- **UI chrome uses Segoe UI.** Appropriate for a Windows desktop app. System font is the right call here, not a web font.
- **Debug log uses Courier New.** Correct monospace choice.

**What keeps this from 9-10:**

- **No weight differentiation in the scale.** The type scale has only size tokens, not weight tokens. Bold is applied ad-hoc via `FontAttributes="Bold"` with no semantic distinction between title weight, subtitle weight, and accent weight.
- **Arabic font is NotoNaskhArabic, not an Indo-Pak variant.** For an app targeting Indo-Pak/Persian script Mushaf display, the ideal typeface would be a Nastaliq or Indo-Pak Naskh variant (like Al Qalam Quran or IndoPak Nastaleeq). NotoNaskhArabic renders correctly but has a standardized, slightly mechanical texture that doesn't match the printed Mushaf aesthetic the 16-liner feature aspires to.
- **HeroVerse 36px has no measured constraint.** It's defined but only used in binding scenarios. The actual verse display uses FontSizeVerse (28px), not HeroVerse.

---

## Interaction Feel — 7/10

The core loop works: start session, speak into mic, see verse match, see confidence, see corrections, stop session. The 80×80 mic button with green/red toggle is the right affordance. Keyboard shortcuts for Space (toggle mic) and Escape (reset) are now implemented via RecitationPage.xaml.cs KeyDown handler — a genuine desktop affordance.

**What works:**

- Model download UX with simultaneous determinate progress + indeterminate spinner is thoughtful.
- Pickers disable during recording to prevent mid-session surah changes.
- Empty states: "No verses practiced yet" on Progress, "Verse hidden — recite from memory" placeholder.
- Transcription shows a "Transcribing…" state with ActivityIndicator.
- Confidence bar provides persistent feedback that survives scrolling.
- The Reset button exists as a session escape hatch.

**What's missing:**

- **No focus rings.** Focus/FocusRing gold tokens are defined in App.xaml but the global Button style is the only element that applies them. Entry, Picker, and the mic button have no visible focus. On a keyboard-driven desktop app, a Tab through the UI is invisible.
- **Disabled states have no visual treatment.** The Surah/Ayah pickers set IsEnabled=false during recording but show no opacity change, no desaturation, no locked icon. The user clicks and nothing happens with no feedback.
- **No confirmation on Reset.** A single misclick on "Reset" wipes the session with no undo. Escape key also triggers reset. The June review flagged this; it's still unresolved.
- **No dark mode means no system theme awareness.** The app ignores the user's OS preference entirely.
- **122 XAML compilation warnings** (XC0022: x:DataType missing). This is a real performance issue — every binding is resolved at runtime via reflection instead of compile-time. The app will feel slower than it should, especially on the Recitation page where bindings fire on every chunk.

---

## Smell Report

| Tell | Where | Severity |
|---|---|---|
| No dark mode in 2026 | Full app | High |
| Confidence bar is green/red only (colorblind fail) | Recitation bottom bar | Medium |
| Info palette = Success palette (semantic collapse) | App.xaml tokens | Medium |
| Five competing bordered panels in scrollable area | RecitationPage middle | Low |
| Collecting ViewModel inside CollectionView with no x:DataType | RecitationPage tajweed list | Low |
| Cramped picker row | RecitationPage top toolbar | Low |

**Prior smells resolved since June review:**
- ✅ Emoji logo → replaced with wordmark + gold accent rules
- ✅ Universal system font → NotoNaskhArabic for Arabic
- ✅ Hex code spam → 51 tokenized Color resources
- ✅ Dead gold color → used in LoginPage, Focus tokens
- ✅ Cool/warm temperature mismatch → unified warm palette
- ✅ Placeholder-only login fields → real labeled fields with ReturnType
- ✅ Stale ConfigureFonts comment → removed from MauiProgram.cs

---

## Recommendations (ordered by impact)

1. **`/design recolor`** — Add dark mode via AppThemeBinding. Wire the existing SurfaceDark/SurfacePanelDark/PrimaryDark tokens. This is the single highest-impact remaining fix — it affects every page and every user on dark OS themes. While there, split Info into a distinct blue/teal palette and add a pattern/shape distinction to the confidence bar for colorblind users.

2. **`/design interaction`** — Apply focus rings to Entry, Picker, and the mic button. Add visual disabled states (opacity 0.5 + locked icon or desaturation) for pickers during recording. Add an Undo-based reset (show toast "Session reset — undo?") instead of instant wipe. This directly resolves the two oldest interaction complaints from the June review.

3. **`/design typeset`** — Add weight tokens to the type scale (FontWeightTitle, FontWeightSubtitle, etc.) so Bold isn't applied ad-hoc. Evaluate an Indo-Pak variant of NotoNaskhArabic or Al Qalam Quran for the Mushaf display. Apply HeroVerse 36px where the scene calls for it (verse match reveal moment).

4. **`/design surface`** — Consolidate the Recitation page middle area. Five conditionally visible bordered panels in a vertical stack is too many. The transcription and tajweed corrections could merge into the verse card as inline annotations. The ASR debug panel should be a collapsed accordion or slide-out, not a full-width bordered box. The confidence bar already has fixed positioning — the remaining panels need clearer priority ordering.

5. **Add compiled bindings.** Add `x:DataType` to all pages (RecitationPage → RecitationViewModel, ProgressPage → ProgressViewModel, etc.) to eliminate the 122 XAML compilation warnings. This is low design effort with high perceived-performance impact.
