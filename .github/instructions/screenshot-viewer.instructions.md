---
applyTo: "WindowWatcher.ScreenshotViewer/**"
---

# Screenshot Viewer — WindowWatcher

These instructions apply to all files under `WindowWatcher.ScreenshotViewer/`.

---

## Purpose

- Browses and plays back screenshots captured by the Daemon (`GraphicsCaptureService`/`ScreenshotCaptureService`), reading files from the configured `ScreenshotsPath` (and `ScreenshotsArchivePath` for archived screenshots) via `SettingsService`.

## Services

- `Services/ScreenshotService` enumerates screenshot files on disk and maps them to `Models/ScreenshotInfo`. Extend this service for new file-discovery logic rather than scanning directories directly from the window/view model code.
- `Services/ImageCacheService` caches decoded images to avoid repeated disk reads/decodes when scrubbing through the timeline — respect this cache when adding new image-consuming UI rather than loading `BitmapImage`s directly.

## Controls

- `Controls/TimelineSlider` (+ `TimelineSliderStyle.xaml`) is the custom scrub control for navigating captured screenshots by time.
- `Controls/HeatMapCanvas` renders a density heat-map of capture activity; like `TimelineCanvas` in the Analyser, it draws directly rather than using per-item WPF controls — keep new visual layers consistent with that approach.

## Window

- `ScreenshotViewerWindow` exposes a context menu for folder/image actions (open folder, copy image details, etc.) — add new file actions to this context menu rather than introducing a separate toolbar unless there's a strong reason.
- Debug-only buttons are gated behind `#if DEBUG` — keep this pattern for any new diagnostic-only UI.
