# WindowWatcher Changelog

## Feature History

### 2026-10-19

**fix: Recover screenshot capture after Direct3D device loss**

- Recreate a lost Direct3D device, clear cached capture resources, and retry capture without restarting the daemon.
- Serialize capture and device disposal so recovery cannot invalidate another capture's device.
- After three consecutive scheduled device-recovery failures, gracefully restart the daemon with its original arguments; the replacement waits for shutdown, and a 15-minute cooldown prevents restart loops.
- Keep one-shot screenshot capture limited to in-process recovery, and leave the current daemon running if the replacement cannot be launched.

**feat: Add auto-launch toggle to daemon settings**

- Settings reads auto-launch status from the current user's Startup folder shortcut, without storing a JSON setting.
- Saving creates or deletes `WindowWatcherDaemon.lnk`, targeting `WindowWatcher.Daemon.exe`; Cancel leaves the shortcut unchanged.

### 2026-01-01

**fix: Restore row titles drawing and row sorting in the Analyser timeline**

- `TimelineCanvas` again draws row title labels alongside each Gantt row.
- `AnalyserViewModel` row-building logic restores the sort-by-duration-descending ordering with `"Idle"` pinned to the top.

### 2025-12-31

**refactor: Rename MainWindow to AnalyserWindow**

- `WindowWatcher.Analyser/MainWindow.xaml(.cs)` renamed to `AnalyserWindow.xaml(.cs)` to distinguish it from the Daemon's own `MainWindow`.
- `App.xaml` and the Daemon's `MainWindow.xaml.cs` updated to reference the renamed window.

**feat: Add screenshot archiving with configurable paths and retention**

- `AppSettings` gained `ScreenshotsArchivePath` and `ScreenshotsArchiveAfterDays` settings.
- `SettingsService` resolves `ScreenshotsArchivePath` as a relative path against the settings directory, same as `ScreenshotsPath`/`DataDirectory`.
- `DataCleanupService` now archives screenshots older than `ScreenshotsArchiveAfterDays` into the archive path before applying retention-based deletion.
- `SettingsWindow` / `SettingsViewModel` expose the new archive path and retention fields for editing.
- `ScreenshotService` (Screenshot Viewer) updated to browse both the active screenshots path and the archive path.

**refactor: Replace all references to "Snapshot" with "Screenshot"**

- Renamed `SnapshotInfo` → `ScreenshotInfo` and `SnapshotService` → `ScreenshotService`.
- Updated all consuming code (`HeatMapCanvas`, `TimelineSlider`, `ScreenshotViewerWindow`, `MainViewModel`, `AppSettings`, `SettingsService`, `DataCleanupService`, `ScreenshotCaptureService`, `SettingsWindow`/`SettingsViewModel`) to use the "Screenshot" naming consistently.

### 2025-12-31

**feat: Screenshot Viewer usability and reliability improvements**

- Added support for passing a screenshots folder argument when initializing the Screenshot Viewer.
- Implemented `LoggingService`-based logging throughout the Screenshot Viewer, replacing `Debug.WriteLine` calls.
- Added a context menu with folder/image actions (open containing folder, copy image details).
- Centered the date controls and right-aligned debug-only buttons (visible only in `DEBUG` builds).
- Refactored the screenshot viewer context menu and updated the application icon.

**feat: JPEG screenshot encoding and capture/logging enhancements**

- Switched the default screenshot image format from PNG to JPEG in `AppSettings`, with corresponding `SettingsWindow` layout adjustments.
- Added JPEG encoding support (`JpegImageEncoder`) alongside the existing PNG encoder.
- Introduced `CapturedImage` to represent a captured screenshot and updated capture logic to use it.
- Enhanced logging across the screenshot and graphics-capture services for better traceability, including a configurable log level and stronger capture diagnostics.
- Added application-wide file-based logging.

**chore: Update target framework and add solution-wide build task**

- Updated all project files to target `net8.0-windows10.0.19041.0`.
- Added a `build: all` task for building the whole solution.

### 2025-12-30

**refactor: Open Analyser/ScreenshotViewer windows in-process**

- The Daemon now opens the Analyser and ScreenshotViewer windows directly in-process instead of spawning them as separate processes.

**feat: Screenshot capture support**

- Introduced initial screenshot capture support and iterated on capture reliability.
- Implemented Direct Injection services for capture.
- Moved activity-logging logic into its own `WindowWatcherService` class and added related settings to `SettingsWindow`.
- Moved `SettingsWindow` into the Daemon project, accessible from the tray context menu; removed the main menu from `ScreenshotViewerWindow` to save space.
- Added run tasks for Daemon, Analyser, and ScreenshotViewer to `tasks.json`.

### 2025-12-16

**refactor: Rename MainWindow to ScreenshotViewerWindow**

- Renamed the Screenshot Viewer's `MainWindow` to `ScreenshotViewerWindow` for clarity across the multi-window solution.

### 2025-12-15

**feat: Multi-project solution restructure, ScreenshotViewer app, and installer/update support**

- Split the codebase into `WindowWatcher.Daemon`, `WindowWatcher.Analyser`, `WindowWatcher.ScreenshotViewer`, and `WindowWatcher.Common` projects (renamed from `WindowWatcherAnalyser`/`WindowWatcherDaemon`).
- Added the `WindowWatcher.ScreenshotViewer` app for browsing folders of screenshots, including a settings dialog.
- Added `ViewEventsWindow`/`ViewEventsViewModel` for viewing raw activity events, with improved error messaging.
- Refactored the Daemon to remove legacy code and adopt the MVVM pattern.
- Updated `AppSettings` paths and settings loading to resolve relative paths correctly.
- Removed the unused `app.config`.
- Finished the Velopack installer upgrade code and added publishing tasks (see [GITHUB_BUILD_INSTALLER_UPDATE_GUIDE.md](../GITHUB_BUILD_INSTALLER_UPDATE_GUIDE.md)).

### 2025-06-12

**perf: Reduce CPU usage by caching process name lookups**

- Cached resolved process names to avoid repeated, costly lookups on every polling iteration.

### 2025-05-24

**fix: CSV log format and timezone handling**

- Converted activity log files from a legacy text format to CSV.
- Converted logged dates to local time including timezone offset.
- Fixed the Daemon to correctly write the `Inactive` event.

### 2021-08-12 and earlier

- Initial import of the project (originally hosted on a private Subversion repository), addition of `.gitignore` files, and the project `LICENSE`.
