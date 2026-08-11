# Taste (Continuously Learned by [CommandCode][cmd])

[cmd]: https://commandcode.ai/

# maui
- After XAML changes and a successful dotnet build, check for MAUI silent failures — XAML parsing errors at runtime that don't appear in the compile step (e.g., missing closing tags like ResourceDictionary, style conflicts, binding path errors, `Color.AppThemeBinding` nested markup resolving to black). Confidence: 0.85
- Keep Android implementation files in the repo but exclude Android from the active build targets — Android is a planned future platform that should not be built yet. Confidence: 0.75

# display
- Display Quran text in 16-liner format (16 lines per page), matching the Indo-Pak/Persian script Mushaf layout. Confidence: 0.85
- Requires full Quran navigation with ayah and juz selection menus — not just page-based browsing or a simplified view. Confidence: 0.80
- Expects professional, polished UI — rejects bland, clunky designs as unacceptable; holds a high bar for visual quality and sophistication. Confidence: 0.85

# recitation
- Keep Whisper for recitation ASR but optimize its usage (e.g., smaller models, faster inference, better chunking) rather than replacing it with lightweight local logic. Confidence: 0.70

# communication
- Prefers status documents and planning summaries in Urdu when the output is meant to be shown to others (stakeholders, non-English-speaking audiences). Confidence: 0.60
- Wants architectural/scope questions re-asked rather than having the assistant auto-decide — when a clarifying question goes unanswered, re-present it instead of choosing unilaterally. Confidence: 0.75

# workflow
See [workflow/taste.md](workflow/taste.md)
# dotnet
See [dotnet/taste.md](dotnet/taste.md)
