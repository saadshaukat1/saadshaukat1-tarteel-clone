# Design Checkup — Tarteel

**Date:** 2026-08-11
**Mode:** checkup
**Register:** Product (offline Windows desktop utility; MAUI .NET 9)
**Surface:** `mobile/TarteelMobile` — Recitation, Mushaf, Progress, Login, and shared `App.xaml` resources
**Score:** 25 / 60

---

## TL;DR

The product structure is coherent and the Windows target builds cleanly, but the rendered Windows surface is not safe to ship: recent screenshots show the application background and most static-resource-driven text resolving to black, leaving the Recitation page unreadable. The next fix must verify runtime resource resolution on the actual Windows app, not only XAML compilation.

**Primary recommendation:** Run `/design recolor` or a focused runtime resource fix. Replace the failing theme-resource path with a verified MAUI-compatible resource strategy and confirm the result visually on Windows.

## Heuristic Scores (/60)

| # | Vital sign | Score | Status | Key finding |
|---|---|---:|---|---|
| 1 | Intentionality | 5/10 | WATCH | The intended green/gold product palette and page composition are clear in XAML, but the rendered result collapses into an unintentional black surface. |
| 2 | Readability | 0/10 | CRITICAL | Screenshots show black page content with labels, picker titles, and controls effectively invisible; the Mic button is the only clearly visible primary element. |
| 3 | Usability | 5/10 | WATCH | The recitation loop has selectors, loading states, feedback panels, and a mic action, but invisible controls prevent reliable operation. |
| 4 | Responsiveness | 5/10 | WATCH | Desktop layout structure is defined and buildable, but runtime rendering is unverified beyond the failing screenshot state; narrow/mobile behavior was not tested in this pass. |
| 5 | Speed | 10/10 | HEALTHY | The required Windows build completed successfully with 0 warnings and 0 errors. Runtime responsiveness was not measured. |
| 6 | Accessibility | 0/10 | CRITICAL | The observed contrast failure blocks low-vision users and makes keyboard focus, labels, and control states impossible to distinguish visually. |

## Signal / Risk

### Watch items

- Static resource colors in `App.xaml` do not match the rendered Windows screenshot.
- The Recitation page depends heavily on shared `StaticResource` values for all labels, surfaces, borders, and picker styling.
- The concrete-color fallback currently fixes compile-time validity but still requires visual runtime confirmation.
- Mobile/narrow-width behavior and keyboard accessibility remain unverified.

### Next modes

- `/design recolor` — verify and repair runtime color/resource resolution.
- `/design interaction` — add visible focus and disabled states after contrast is reliable.
- `/design responsive` — verify the desktop composition at narrow widths and touch input sizes.

## What's Working

- **Composition matches the task.** Recitation is organized as selector controls, a verse/feedback workspace, and a persistent mic/status action.
- **Arabic presentation is intentional.** The page uses `NotoNaskhArabic`, right-to-left flow, a large verse size, and a bounded reading measure.
- **State coverage exists.** Model missing/downloading, transcription, mismatch, tajweed, hidden verse, and reset states are represented in XAML.
- **Build health is strong.** The Windows target compiles with no warnings or errors after the recent resource edits.

## Priority Issues

### P0 — Runtime palette resolves to black

**Evidence:** The supplied Windows screenshots show a nearly black page where the Recitation heading, selector labels, picker fields, borders, and status text cannot be read. The Mic button remains visible because its color comes from a direct view-model binding rather than the shared static palette.

**Why it matters:** This blocks the primary task and invalidates the contrast/accessibility baseline regardless of successful compilation.

**FIX:** Verify each shared resource at runtime on Windows. Use a known-good MAUI resource pattern, add an unmistakable temporary diagnostic color if necessary, and do not close the issue until a fresh screenshot shows readable labels, picker text, surfaces, and borders.

### P1 — Compile success is being mistaken for visual success

**Evidence:** The Windows build reports 0 warnings and 0 errors, while the supplied runtime screenshots remain unreadable.

**Why it matters:** XAML compilation cannot validate the effective color values produced by the runtime resource dictionary and native WinUI control rendering.

**FIX:** Add a manual visual verification step to the recolor pass: launch the Windows app, inspect Recitation/Progress/Mushaf, toggle the OS theme if supported, and capture the actual rendered states.

### P2 — Focus and disabled states remain visually unverified

**Evidence:** Picker and button visual states are defined in `App.xaml`, but the screenshot does not show readable focus, disabled, or hover treatment.

**Why it matters:** A desktop app needs visible keyboard and interaction feedback, especially when pickers are disabled during recording.

**FIX:** Recheck focus rings and disabled opacity only after the base palette is visibly working.

### P2 — Narrow-width and touch behavior unverified

**Evidence:** The page uses a two-column desktop grid with fixed secondary-column width and tightly grouped selectors; no narrow-width screenshot or interaction trace was provided.

**Why it matters:** The same composition may clip or make controls too small on mobile or a narrow desktop window.

**FIX:** Run a responsive pass at narrow desktop and mobile widths, preserving the complete recitation workflow.

## Verdict

**PAUSE visual work on new features.** The app needs a runtime contrast/resource fix and fresh Windows screenshots before further polish or accessibility work can be trusted.
