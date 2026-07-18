# Taste (Continuously Learned by [CommandCode][cmd])

[cmd]: https://commandcode.ai/

# maui
- After XAML changes and a successful dotnet build, check for MAUI silent failures — XAML parsing errors at runtime that don't appear in the compile step (e.g., missing closing tags like ResourceDictionary, style conflicts, binding path errors). Confidence: 0.70
- Keep Android implementation files in the repo but exclude Android from the active build targets — Android is a planned future platform that should not be built yet. Confidence: 0.65

# display
- Display Quran text in 16-liner format (16 lines per page), matching the Indo-Pak/Persian script Mushaf layout. Confidence: 0.75

# recitation
- Keep Whisper for recitation ASR but optimize its usage (e.g., smaller models, faster inference, better chunking) rather than replacing it with lightweight local logic. Confidence: 0.70

# dotnet
- LocalRecitationCore must target .NET 9.0 (matching the global.json SDK version 9.0.315), not .NET 10.0. Confidence: 0.70
- When retargeting one project's TFM for SDK compatibility, check and fix all projects in the solution — don't leave any project targeting an unsupported SDK. Confidence: 0.65

