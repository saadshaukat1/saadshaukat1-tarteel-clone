# Tarteel Desktop design brief

## Register

**Product.** Tarteel is a Windows-first, offline Quran recitation coach for a maktab student. The interface is an instrument used repeatedly during practice, not a marketing surface.

## Users and context

- **Primary user:** a Quran student practicing hifz and tajweed, often reciting aloud while looking between the Mushaf and feedback.
- **Secondary context:** a parent or teacher reviewing progress and recurring errors.
- **Environment:** Windows desktop, local/offline operation, microphone in use, Arabic content, keyboard and mouse available. Android files remain future-platform implementation and are not the active design target.
- **User pressure:** maintain concentration, know what to recite next, distinguish authoritative verse text from generated feedback, and recover quickly from a mistake.

## Product purpose

Make the next correct recitation action obvious. The product must guide a student through a passage, listen locally, match the spoken words to the expected ayah, surface actionable tajweed coaching, persist the attempt, and recommend the next review item.

The central artifacts are:

- a **recitation workspace** with expected ayah, live transcription, confidence, mismatches, and tajweed guidance;
- a **Mushaf page** with 16-line reading rhythm and ayah location;
- a **Today review queue** with due assignments and the next action;
- a **practice record** showing mastery, attempts, and recurring weak areas.

## Dominant composition lanes

Each surface must declare one dominant work pattern before layout decisions:

- **Recitation: Monitor + Operate.** Persistent status dock, dominant ayah workspace, live feedback, and always-available recording control. Do not let diagnostics outrank the expected verse.
- **Today / Progress: Decide + Compare.** Put the highest-priority due assignment and next action first, followed by a scannable queue and concise evidence of progress.
- **Mushaf: Learn + Explore.** Reading measure and authentic page rhythm dominate. Navigation remains available but subordinate to the page.
- **Login / setup: Configure.** Short, labeled form with one clear commit action and explicit offline/model readiness states.

Avoid generic centered hero layouts, marketing card grids, and pill-heavy dashboard patterns. Use cards only for genuinely independent artifacts such as a due assignment, error cluster, or model alert. Prefer flat reading lanes and dividers for related content.

## Voice and writing

Calm, precise, reverent, and teacher-like without pretending to replace qualified human judgment. Use short sentence-case labels and one verb per action: “Start recitation”, “Load passage”, “Reveal verse”, “Open Mushaf”, “Review errors”.

Feedback names the observed issue and the next correction. Empty states explain what belongs there and how to create it. Loading states name the actual work, such as importing a model or transcribing audio. Avoid exclamation points, hype, gamified language, and claims that heuristic or Whisper-derived feedback is authoritative tajweed judgment.

## Anti-references

Do not drift toward:

- generic SaaS dashboards with excessive rounded cards and decorative gradients;
- blue-violet technology branding or neon AI aesthetics;
- a mobile app stretched across a desktop window;
- emoji branding, icon-only unexplained controls, or text-only navigation where an established icon asset exists;
- dense terminal-like diagnostic UI as the main experience;
- algorithmically wrapped Quran text presented as authentic Mushaf line data;
- green/red-only semantic communication that fails color-vision users;
- hidden labels, placeholder-only forms, or hover-only functionality.

## Visual foundation

The existing authored foundation is the source of truth:

- **Surface:** warm parchment light theme, with the existing dark-theme tokens wired through `AppThemeBinding` rather than introducing a second palette.
- **Primary:** deep Quranic green for active actions and stable navigation.
- **Accent:** restrained gold for focus, dividers, and moments of attention, not broad decoration.
- **State roles:** distinct success, error, warning, and blue information families. Confidence must remain distinguishable by color plus text, icon, or shape.
- **Typography:** Segoe UI for Windows UI chrome; `NotoNaskhArabic` for Arabic/Quran text; Courier New only for diagnostics. Arabic requires RTL flow, generous diacritic-safe line height, and a constrained readable measure.
- **Scale:** use the existing named type, line-height, radius, shadow, surface, border, and state resources in `App.xaml`. Do not add page-level hex values or ad-hoc sizes without a real content reason.
- **Depth:** the Mushaf parchment/page is the strongest decorated surface. Primary workspace surfaces may use restrained elevation; secondary panels should flatten and use hierarchy through spacing, type, tint, or dividers.

## Component rules

- Every interactive control needs idle, hover, pressed, focused, loading, empty, error, disabled, and overflow behavior where applicable.
- Preserve visible keyboard focus with a 2–3px gold focus ring and a clear offset. Never remove outlines without an equivalent.
- Keep interactive hit areas at least 44×44px; the recording action may be larger because it is the primary control.
- Show disabled picker/control treatment during recording through opacity and state styling, not silent non-response.
- Keep the recording/status dock persistent while the student scrolls. It must expose the current status, recording action, confidence, and safe recovery action.
- Keep the expected ayah visually primary. Transcription is subordinate evidence; mismatches and tajweed coaching are corrective annotations, not competing hero cards.
- Use compiled bindings consistently on pages and collection templates. Prefer explicit semantic properties over string-delimited converter color parameters.
- Navigation uses the existing semantic SVG icons for Recite, Mushaf, and Progress with readable titles; icons never replace necessary labels.
- Support long Arabic text, long English diagnostics, empty queues, model-not-ready, transcription-in-progress, low-confidence, error, and offline states with no clipped or ambiguous content.

## Accessibility and motion

- Light and dark themes must preserve contrast for Arabic text, status colors, focus rings, and disabled controls.
- Do not encode correctness or error by color alone. Pair state color with text, iconography, position, or explicit labels.
- Support keyboard traversal for the desktop workflow: passage selection, start/stop recording, reveal/hide verse, reset/recover, Mushaf navigation, and login submission.
- Respect reduced-motion preferences. Motion should be restrained and functional, never distracting during recitation.
- Use logical layout properties and correct RTL behavior for Arabic content. Mirror directional navigation icons when direction is semantic; do not mirror universal play/check symbols.

## Design debt to keep visible

The current audits identify these as follow-up priorities, not reasons to invent a new visual language:

1. Complete dark-theme application across all surfaces.
2. Finish focus and disabled-state treatment for Entry, Picker, and recording controls.
3. Reduce the Recitation middle stack so the verse remains the single source of truth.
4. Replace algorithmic 16-line wrapping with verified line-level Mushaf data when available.
5. Continue applying compiled bindings and semantic color properties consistently.
