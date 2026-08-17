# Feature Plan: Responsive Android UI Layout & High-Speed Model CDN Distribution

## Overview
This document specifies the technical plan and architecture for:
1. **Responsive Android Layout & Button Ergonomics**: Optimizing all MAUI XAML views for mobile touch targets (min 48dp), screen density adaptability, notch/safe-area handling, and keyboard avoidance.
2. **High-Speed Model CDN Distribution**: Hosting and streaming Whisper.net model binaries (`ggml-tiny.bin`, `ggml-base.bin`, etc.) via edge CDNs with zero egress costs and high throughput.

---

## 1. High-Speed Model CDN Distribution Architecture

Whisper model binaries range from 75 MB (`tiny`) to 142 MB (`base`) and 466 MB (`small`). Providing reliable, zero-throttle direct download links is critical for instant mobile onboarding and background model tier upgrades.

### 🚀 Recommended Solutions

#### Option 1: GitHub Releases (Easiest & Most Reliable)
GitHub Releases are backed by Fastly and Azure CDNs with unlimited download traffic for public repositories.
1. In your GitHub repo, go to **Releases** $\rightarrow$ **Draft a new release**.
2. Create a tag (e.g., `v0.1.0-models`).
3. Drag and drop `ggml-tiny.bin`, `ggml-base.bin`, etc., into the release assets.
4. Click **Publish release**.
5. Your permanent CDN direct download URLs will be:
   ```text
   https://github.com/<username>/<repo>/releases/download/v0.1.0-models/ggml-tiny.bin
   https://github.com/<username>/<repo>/releases/download/v0.1.0-models/ggml-base.bin
   ```
   *(GitHub automatically issues a 302 redirect to `objects.githubusercontent.com` Fastly CDN).*

#### Option 2: Cloudflare R2 (Fastest Global Edge & Zero Egress Fees)
Cloudflare R2 provides S3-compatible object storage with $0 egress fees across 300+ global data centers.
1. Sign up for a free [Cloudflare](https://dash.cloudflare.com/) account $\rightarrow$ **R2 Object Storage**.
2. Create a bucket (e.g., `tarteel-models`).
3. Upload your `.bin` model files.
4. Enable **Public Development URL** or connect your custom domain (e.g., `https://models.yourdomain.com`).
5. Your permanent download URLs:
   ```text
   https://pub-xxxxxx.r2.dev/ggml-tiny.bin
   https://pub-xxxxxx.r2.dev/ggml-base.bin
   ```

### Implementation in Configuration (`appsettings.json`)
```json
{
  "Asr": {
    "PrimaryTier": "tiny",
    "FallbackTier": "base",
    "ModelUrls": {
      "tiny": "https://github.com/saadshaukat1/saadshaukat1-tarteel-clone/releases/download/v0.1.0-models/ggml-tiny.bin",
      "base": "https://github.com/saadshaukat1/saadshaukat1-tarteel-clone/releases/download/v0.1.0-models/ggml-base.bin"
    }
  }
}
```

---

## 2. Responsive Android Layout & Button Ergonomics

### Problem Statement
On mobile Android devices (360dp–430dp viewport widths), current UI views experience:
- **Button Crowding & Truncation**: Header toolbars and bottom session controls wrap awkwardly or clip on compact screens.
- **Touch Target Deficiencies**: Certain buttons and picker dropdowns do not meet the Android Material 48dp touch target minimum.
- **Nested Scroll Traps**: Nested `CollectionView` elements inside `ScrollView` cause scroll hijacking and touch conflicts on Android touchscreens.
- **Safe Area & System Bar Insets**: Bottom action bars risk collision with gesture navigation bars and camera cutouts.
- **Arabic Text Scaling**: Large 36px hero verse fonts overflow viewport bounds on compact phone displays.

### UI & Layout Enhancements

#### A. Global Styles & Touch Ergonomics (`App.xaml`)
- Standardize all interactive controls (`Button`, `Picker`, `Entry`) with `MinimumHeightRequest="48"` and `MinimumWidthRequest="48"`.
- Implement responsive font scaling (e.g., `FontSizeHeroVerse` scaled dynamically for compact viewports).
- Add high-contrast Android visual state feedback (`PointerOver`, `Pressed`, `Focused`).

#### B. Recitation Page (`RecitationPage.xaml`)
- **Header**: Refactor `HeaderGrid` with dynamic wrap or compact icon buttons to prevent header clipping on narrow screens.
- **Passage Selector**: Flex/Grid layout adapting `Surah` and `Ayah` pickers to 50/50 split on mobile with full-width `Load` button.
- **Arabic Verse Display**: Responsive text measure with RTL container centering, adapting font size between 26–32px on mobile and 36px on tablet/desktop.
- **Diagnostics Panel**: Flatten nested collections or use dynamic auto-sizing grids to eliminate scroll trapping.
- **Bottom Session Console**:
  - Sticky bottom container with Android safe area insets.
  - Large primary mic action button (56–60dp height, pill shape) with thumb-zone ergonomics.
  - Quick action toolbar adapting neatly into a balanced 2x2 grid or horizontal scroll on compact screens.

#### C. Mushaf Page (`MushafPageView.xaml`)
- **Navigation Header & Jump Bar**: Fluid grid columns ensuring Juz/Surah/Ayah pickers fit without horizontal overflow.
- **16-Line Container**: Dynamic density calculation for row heights (`42–48dp` on mobile) ensuring readable Arabic text and smooth scroll-to-advance.
- **Navigation Controls**: 48x48dp tap targets for Prev/Next page buttons.

#### D. Progress & Practice Record (`ProgressPage.xaml`)
- Single-column stacked summary card layout on Android mobile.
- Clean list layouts for `TodayAssignments` and `WeakVerses` without nested scroll conflicts.
- 48dp touch targets for "Start" and "Refresh" action buttons.

#### E. Startup & Login Pages (`StartupPage.xaml`, `LoginPage.xaml`)
- `KeyboardAware` scroll containers preventing soft keyboard obstruction on Android.
- Full-width touch-friendly CTA buttons (50dp height).
