# Design Review — Tarteel Desktop

**Date:** 2026-06-10
**Score:** 24/50
**Register:** Product (desktop utility app)

---

## First Impression — 5/10

Tarteel lands as a competent utility with an appropriate color direction — warm parchment surface, deep green primary, gold accent. The "🕌 Tarteel" emoji logo on the login page immediately signals "placeholder identity." The universal Segoe UI system font across both UI chrome and Arabic Quran text reads as "I didn't think about type at all." The surface feels functional but anonymous — it could be any hobbyist Quran tool. No visual signature survives beyond the green + cream combo.

**Primary failure:** No distinctive visual voice. The palette points in the right direction but the typography, spacing rhythm, and surface treatment don't back it up.

---

## Hierarchy — 6/10

The Recitation page correctly places the voice-control (mic button, 80×80 circle) as the dominant action at bottom-center. The confidence progress bar sits persistently below the scroll area — smart, always-visible feedback. The verse panel is given 28px Arabic text as the hero content unit.

The verse selector row (Surah picker, Ayah picker, Load button) is cramped — three controls in one row with only 8px column spacing. The 60px-wide Load button feels pinched. The model-setup overlays (missing model, downloading) occupy significant visual weight and break the flow, though they're temporary states.

The Progress page has a clean list layout but the page title at 22px (vs Recitation's 18px) is an inconsistency that feels unintentional.

**Primary issue:** The scrollable middle area contains 6 distinct bordered panels (transcription, verse, placeholder, tajweed, ASR debug) — too many competing surfaces occupying the same visual plane with no clear priority ordering beyond stacking.

---

## Color Voice — 5/10

The tokenized palette is coherent on paper: `#1A6B3C` (primary green), `#0F3D22` (dark green), `#C9A84C` (gold), `#F9F6EF` (warm cream surface), `#1C1C1E` (dark text), `#E53935` (error red). This is an Islamic-app palette that reads as Quranic without being gaudy.

But the implementation undermines it:

- **Gold (#C9A84C) is defined but never used.** Not once in any XAML file. Dead color.
- **Cool Tailwind grays pollute the warm surface.** The transcription panel uses `#F9FAFB` (cool white), `#D1D5DB` (cool gray border), `#374151` (cool dark gray text). These clash with the `#F9F6EF` warm cream. The app has two competing color temperatures.
- **Hex code spam.** Dozens of hardcoded hex values (`#6B7280`, `#9CA3AF`, `#3B82F6`, `#059669`, `#DC2626`, `#991B1B`, `#7F1D1D`, `#1D4ED8`, `#E8F5E9`, `#FFF8F8`, `#FFFBEB`, `#FEF2F2`, `#F0FDF4`, `#111827`, `#D1FAE5`, `#EFF6FF`, `#92400E`, `#B45309`, `#78350F`, `#F59E0B`, `#2563EB`, `#CCCCCC`, `#F0F0F0`) are scattered across XAML with no token names. Only 4 colors (Primary, PrimaryDark, Gold, Surface, OnSurface, Error) are tokenized, but ~30+ are not.
- **No dark mode.** Not a single dark theme resource, not even `AppThemeBinding`.
- **No colorblind considerations.** Deuteranopia and protanopia will merge the green confidence bar with everything.

---

## Type Voice — 3/10

This is the weakest lens by a margin.

- **Segoe UI for Arabic is not acceptable.** Arabic script requires proper Arabic typefaces with correct letter joining, ligatures, diacritic positioning, and contextual shaping. Segoe UI renders Arabic poorly — characters disconnect, diacritics float, and the overall texture is mechanical, not Quranic. The app is centered on Arabic recitation but uses the wrong tool to display it.
- **No Arabic font fallback chain.** `ArabicFontFamily` is set to `Segoe UI` — the same as `UiFontFamily`. There is no distinction.
- **No measure control.** The 28px Arabic verse text has no max-width constraint, no line-length management. On wide desktop screens, a single ayah could span 200+ characters per line — unreadable.
- **System font everywhere.** Segoe UI is a reasonable UI chrome font but using it for everything — titles, body, Arabic, debug logs — signals zero typographic investment.
- **The debug log uses Courier New** at 11px on a dark background (#111827). This is correct for monospace debugging and actually the best typographic decision on the page.

---

## Interaction Feel — 5/10

**What works:**
- 80×80 mic button is properly touch-friendly (exceeds 48px minimum).
- Model download UX: simultaneous determinate progress bar + indeterminate spinner, toggled by `IsModelDownloadIndeterminate`. This is thoughtful — the app handles the case where total model size is unknown.
- Pickers are disabled during recording (preventing surah changes mid-session).
- Confidence bar provides persistent visual feedback at the bottom of the screen.
- Empty states exist: CollectionView `EmptyView` on progress page, "Verse hidden" placeholder on recitation page.
- The Reset button exists as a session escape hatch.

**What's missing:**
- **No keyboard shortcuts.** Desktop-first Windows app with zero keyboard support — no hotkeys for mic toggle, surah navigation, reset.
- **No focus management.** No visible focus rings, no TabIndex ordering, no keyboard navigation through the verse selector.
- **No disabled visual states.** The Surah/Ayah pickers become disabled during recording but show no visual change — opacity, desaturation, or placeholder text. The user clicks and nothing happens with no feedback.
- **Reset has no confirmation.** A single misclick on Reset wipes a session with no undo, no confirm dialog.
- **Login page has placeholder-only fields.** "Email" and "Password" are placeholders, not labels. They disappear on focus. There's no Enter-to-submit behavior.
- **No loading state for verse loading.** If `LoadSelectedVerseCommand` does a DB lookup that takes time, there's no spinner, no skeleton, no indication.
- **No error recovery for transcription failures.** When Whisper fails, the diagnostic message appears in the debug log (if advanced mode is on) but there's no user-facing error state.
- **The Show/Hide Verse button behavior is a toggle** with no undo path — it's fine for this use case (show/hide instantly reversible) but the label changes between states, which is good.

---

## Smell Report

| Tell | Where | Severity |
|---|---|---|
| Emoji logo as brand identity | Login page "🕌 Tarteel" | High |
| Universal system font | Every page | Critical |
| Predictable Islamic-app palette | Full palette | Medium |
| Cool/warm color temperature mismatch | Transcription panel vs. surface | Medium |
| Hex code spam instead of tokens | Throughout XAML | Medium |
| Dead palette color (Gold unused) | App.xaml token | Low |

---

## Recommendations (ordered by impact)

1. **`/design typeset`** — Replace Segoe UI Arabic with a proper Arabic typeface (`Noto Naskh Arabic`, `Amiri`, or `Scheherazade New`). Set measure for 28px Arabic text. Distinguish UI font from Arabic font with a real fallback chain. This is the single highest-impact fix.

2. **`/design recolor`** — Tokenize the 30+ hardcoded hex values. Replace cool grays with warm-tinted equivalents. Actually use the Gold accent color. Add `AppThemeBinding` for dark mode support.

3. **`/design relayout`** — Re-compose the Recitation page middle area. Six bordered panels in a vertical stack is too many. The confidence bar and action buttons already have fixed positions; the scrollable area should consolidate its panels into fewer, more distinct surfaces.

4. **`/design interaction`** — Add keyboard shortcuts for core actions (Space to toggle mic, Escape to reset). Add focus rings and TabIndex ordering. Add disabled visual states for pickers. Add confirmation dialog for Reset.

5. **`/design voice`** — Replace the emoji logo with a real brand mark. Decide whether this is a utility (lean, efficient, tool-like) or a spiritual companion (warm, reverent, crafted) — the current design splits the difference and commits to neither.
