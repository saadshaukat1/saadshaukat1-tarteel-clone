# Design Checkup — Tarteel (Offline Recitation Desktop App)

**Date:** 2026-07-15
**Mode:** checkup
**Register:** Product (offline Windows desktop utility; MAUI .NET 9, also builds for Android)
**Surface:** `mobile/TarteelMobile` — Recitation, Progress, Login pages + `App.xaml` token system
**Score:** 45 / 60

---

## TL;DR

The app is in much better shape than the June 24/50 review suggested. The token system, Arabic typography, real form labels, and visible states have all been implemented. The remaining risks are accessibility (no dark mode, no keyboard paths, no focus rings), one stale code contradiction around the Arabic font, and an unverified speed signal because the build is blocked by an unrelated .NET 10 SDK mismatch. Proceed to `/design interaction` and `/design recolor` (dark-mode + cleanup), then revisit.

---

## Heuristic Scores (/60)

| # | Vital sign | Score | Status | Key finding |
|---|---|---|---|---|
| 1 | Intentionality | 8 | Watch | Coherent, authored palette and type scale; isolated stale contradiction in `MauiProgram.ConfigureFonts` (empty with a comment claiming fonts are not yet added, while `csproj` does register `NotoNaskhArabic.ttf`). |
| 2 | Readability | 9 | Healthy | Arabic renders via `NotoNaskhArabic` with 28px hero, 1.8 line-height, 700px max-width measure. Body copy uses a consistent tokenized type scale. |
| 3 | Usability | 7 | Watch | Core recitation loop is complete: mic (80px), confidence bar, verse visibility toggle, reset, model-missing/download overlays, empty states. No keyboard shortcuts or focus rings for a desktop-first app. |
| 4 | Responsiveness | 8 | Healthy | Single-window desktop layout; Arabic `MaximumWidthRequest` + `LineBreakMode="WordWrap"` prevents runaway lines. Mobile target (Android) has no verified responsive pass. |
| 5 | Speed | 5 | Watch | Unverified. Build blocked by environment: referenced `LocalRecitationCore` targets .NET 10.0 while installed SDK is 9.0.315. Cannot confirm runtime smoothness, layout shift, or font load this pass. |
| 6 | Accessibility | 8 | Watch | Color tokens are high-contrast (green/cream, red on light bg). Gaps: no dark mode/`AppThemeBinding`, no visible focus, no keyboard nav, no reduced-motion handling, picker disabled state has no visual treatment. |

---

## What's Working

- **Token system is real.** 51 named `Color` resources in `App.xaml`; no page-level hex spam (the 4 inline hex values in RecitationPage live inside `BoolToColorConverter` `ConverterParameter` strings, which is required by the converter, not drift).
- **Arabic typography is correct.** `NotoNaskhArabic` is registered via `csproj` `<MauiFont Include="Resources/Fonts/*.ttf" />` and applied with a proper fallback, 1.8 line-height, and 700px measure. This directly answers the June review's #1 critical complaint.
- **Login form is fixed.** Real `<Label>` fields ("Email", "Password"), not placeholder-only. `ReturnType`/`ReturnCommand` wired for Enter-to-submit. Emoji logo from the June review is gone — replaced by a wordmark plus gold accent rules.
- **States exist.** Model-missing and model-downloading overlays, transcription "Transcribing…" indicator, tajweed corrections panel, empty `CollectionView` on Progress, verse hidden placeholder, error label on login.

---

## Priority Issues

**P1 — Stale font-registration contradiction**
`MauiProgram.cs` `ConfigureFonts` is empty with the comment "Keep default system fonts until packaged font assets are added," but `TarteelMobile.csproj` already registers `Resources/Fonts/*.ttf` (which includes `NotoNaskhArabic.ttf`). The comment is wrong and will mislead the next contributor into "fixing" working font code. Remove the empty `ConfigureFonts` block and the stale comment so the registration truth is the csproj.

**P1 — No dark mode / `AppThemeBinding`**
Every color is a single light value. `SurfaceDark`/`SurfacePanelDark` tokens exist but are unused. Desktop users on dark OS themes get no adaptation, and the green confidence bar + debug panel lose context. Add `AppThemeBinding` scaffolding (or a manual theme) and wire the existing `*Dark` tokens.

**P2 — No keyboard paths on a desktop-first app**
No `KeyboardAccelerator` or `KeyUp` handlers. Core actions (toggle mic, reset, navigate tabs, submit login) are mouse/touch-only. Add Space → toggle mic, Esc → reset, Enter → submit, and Tab order through the verse selector.

**P2 — No visible focus or disabled-state treatment**
`App.xaml` defines `Focus`/`FocusRing` gold tokens but nothing applies them. The Surah/Ayah pickers disable during recording with no visual change (opacity/desaturation). Add focus rings and a disabled visual state so keyboard and disabled users get feedback.

**P2 — Speed unverified due to build block**
The project references `LocalRecitationCore` targeting `net10.0`, but the active SDK is 9.0.315, so `dotnet build` fails with `NETSDK1045`. This is an environment/SDK mismatch, not a design defect, but it prevents verifying runtime speed, layout shift, and font-load behavior. Resolve the SDK target (or install .NET 10 SDK) before the next visual pass.

---

## Next Modes

- `/design interaction` — keyboard shortcuts, focus rings, disabled states, reduced-motion. Highest-impact remaining fix.
- `/design recolor` — `AppThemeBinding` dark mode, apply the unused `*Dark` tokens, retire the stale `ConfigureFonts` comment.
- `/design relayout` (lighter) — only if the Recitation middle stack still feels crowded; the June "six panels" concern is mitigated by conditional `IsVisible` but worth a re-look after interaction work.
