# GitHub Copilot Instructions — WindowWatcher

This file provides project-wide instructions for GitHub Copilot. Read it before generating or modifying any code.
Scoped instructions for specific areas of the codebase are in `.github/instructions/`.

---

## Project Overview

WindowWatcher is a **Windows desktop application suite** (WPF, .NET 8, C#) that runs in the background to track window activity (which process/window is focused and for how long) and periodically capture screenshots. It ships as three separate executables that share a common library.

### Solution Structure

```
WindowWatcher.sln
├── WindowWatcher.Daemon/           # Background tray app — the main entry point
│   ├── App.xaml(.cs)              # Startup, DI container, Velopack update checks, CSV migration
│   ├── WindowWatcherService.cs    # Core polling loop that detects window/foreground changes
│   ├── UserActivityHook.cs / UserActivityService.cs   # Win32 idle/activity detection
│   ├── GraphicsCaptureService.cs  # Windows.Graphics.Capture based screenshot capture
│   ├── ScreenshotCaptureService.cs, FastPngEncoder.cs, JpegImageEncoder.cs, CapturedImage.cs
│   ├── DataCleanupService.cs      # Retention/cleanup of old logs and screenshots
│   ├── MainWindow / SettingsWindow / ViewEventsWindow (+ .xaml)
│   └── ViewModels/
├── WindowWatcher.Analyser/         # Timeline/analysis viewer for activity CSV data
│   ├── AnalyserWindow.xaml(.cs)
│   ├── Controls/TimelineCanvas.cs # Custom-drawn Gantt-style timeline
│   └── ViewModels/AnalyserViewModel.cs
├── WindowWatcher.ScreenshotViewer/ # Browses/plays back captured screenshots
│   ├── ScreenshotViewerWindow.xaml(.cs)
│   ├── Controls/ (HeatMapCanvas, TimelineSlider)
│   ├── Services/ (ImageCacheService, ScreenshotService)
│   ├── Models/ScreenshotInfo.cs
│   └── ViewModels/
└── WindowWatcher.Common/           # Shared class library — no window-specific UI code
    ├── Models/ (AppSettings, WindowActivityEvent)
    ├── Services/ (CsvDataService, CsvMigrationUtility, LoggingService, SettingsService, WindowsApiService)
    └── Utilities/WindowActivityEventUtilities.cs
```

The Daemon opens the Analyser and ScreenshotViewer windows directly in-process (via `ProjectReference`), however these can also be opened as a separate process.

### Technology Stack

| Component | Library |
|-----------|---------|
| UI framework | WPF (.NET 8, `net8.0-windows10.0.19041.0`, Windows-only) |
| Tray icon | Hardcodet.NotifyIcon.Wpf |
| DI container | Microsoft.Extensions.DependencyInjection |
| Logging | Microsoft.Extensions.Logging.Abstractions + custom `LoggingService` (file-based) |
| Screenshot capture | Windows.Graphics.Capture (via `GraphicsCaptureService`) |
| Auto-update / installer | Velopack |
| Settings/data storage | JSON (`System.Text.Json`) settings file, CSV activity logs |
| MVVM | Hand-rolled (`INotifyPropertyChanged`, `RelayCommand` — no external MVVM framework) |

---

## Changelog Maintenance

**After every code change, update `docs/changelog.md`.**

- Add an entry under the current date (ISO format `YYYY-MM-DD`) at the top of the _Feature History_ section.
- Follow the existing format: `**feat/fix/refactor: Short description**` followed by bullet points for the details.
- Do not skip this step. Keeping the changelog current is as important as the code change itself.

---

## Self-Improving Instructions

When a follow-up message contains phrases like:
- "you forgot to …"
- "always do …" / "always use …"
- "never do …" / "never use …"
- "remember to …"
- "make sure you …"

…treat it as a lesson that should be captured permanently. After applying the correction:
1. Identify which `.github/instructions/*.instructions.md` file is most relevant to the topic.
2. Add a concise rule or note to that file so the same mistake is not repeated.
3. If no existing scoped file fits, add the rule to this file under a new section.

**Keep instructions files lean.** Reference actual source files rather than duplicating code samples — code blocks in instructions files go stale and take up context.

---

## Cross-Cutting Rules

### Architecture
- Keep all shared models, services, and utilities with no WPF/window-specific logic in `WindowWatcher.Common`.
- Each of the three executable projects (`Daemon`, `Analyser`, `ScreenshotViewer`) may reference `WindowWatcher.Common`. `Daemon` also references `Analyser` and `ScreenshotViewer` so it can open their windows directly.
- Target framework for every project is `net8.0-windows10.0.19041.0` — do not change this without checking all four `.csproj` files.

### MVVM / UI
- View models implement `INotifyPropertyChanged` manually and call `OnPropertyChanged([CallerMemberName])`; commands use the shared `RelayCommand` (`ICommand`) pattern — see [AnalyserViewModel.cs](../WindowWatcher.Analyser/ViewModels/AnalyserViewModel.cs).
- Do not introduce a third-party MVVM framework (e.g. CommunityToolkit.Mvvm, Prism) without discussion — the codebase intentionally keeps this dependency-free.
- WPF windows use plain `Window` (no MahApps/Metro theming in this project, unlike some other projects).

### Code Style
- Use `System.Text.Json` for settings/config serialisation — see `SettingsService`.
- Activity events are persisted as CSV via `CsvDataService`, not JSON — see [WindowActivityEvent.cs](../WindowWatcher.Common/Models/WindowActivityEvent.cs) `ToCsvString()`.
- Use `async/await` for I/O and long-running work; avoid blocking the UI thread.
- Logging goes through `LoggingService` (implements `ILogger`), which writes to `%APPDATA%\WindowWatcher\Logs`. Prefer injecting `LoggingService`/`ILogger` over `Debug.WriteLine` or `Console.WriteLine`.

### Settings & Storage
- User settings are stored as JSON at `%APPDATA%\WindowWatcher\settings.json`, loaded/cached by `SettingsService`. Relative paths in `AppSettings` (e.g. `ScreenshotsPath`, `DataDirectory`) are resolved against the settings directory.
- Activity logs are CSV files under the configured `DataDirectory`; screenshots are stored under the configured `ScreenshotsPath` (with optional archiving to `ScreenshotsArchivePath`).
- When adding a new setting to `AppSettings`, update its `Clone()` method to include the new field.

### Build & Run
- Build the whole solution with the `build: all` task (`dotnet build WindowWatcher.sln`), or build/run individual projects with the corresponding `build:` / `run:` tasks in `.vscode/tasks.json`.
- Publishing/installer tasks (`build: publish all`, `build: installer`) use `dotnet publish --self-contained -r win-x64` and Velopack's `vpk pack`.
- See [GITHUB_BUILD_INSTALLER_UPDATE_GUIDE.md](../GITHUB_BUILD_INSTALLER_UPDATE_GUIDE.md) for the Velopack/GitHub Actions release pipeline.

---

## Scoped Instructions

For area-specific rules, see:

| File | Applies to |
|------|-----------|
| [common.instructions.md](instructions/common.instructions.md) | `WindowWatcher.Common/**` |
| [daemon.instructions.md](instructions/daemon.instructions.md) | `WindowWatcher.Daemon/**` |
| [analyser.instructions.md](instructions/analyser.instructions.md) | `WindowWatcher.Analyser/**` |
| [screenshot-viewer.instructions.md](instructions/screenshot-viewer.instructions.md) | `WindowWatcher.ScreenshotViewer/**` |
