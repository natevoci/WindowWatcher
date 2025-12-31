---
applyTo: "WindowWatcher.Daemon/**"
---

# Daemon — WindowWatcher

These instructions apply to all files under `WindowWatcher.Daemon/`.

---

## Startup & DI

- `App.xaml.cs` is the composition root: it builds the `ServiceCollection`/`ServiceProvider`, initialises Velopack (`VelopackApp.Build().Run()`), checks for updates (only for installed production builds — see `ShouldCheckForUpdates()`), and runs `CsvMigrationUtility.MigrateCsvFilesIfNeeded()` before the tray icon/UI starts.
- New long-running services should be registered in `ConfigureServices` and resolved via constructor injection, following the pattern in `WindowWatcherService`.
- The Daemon supports a one-shot CLI mode (`-- CaptureSingleScreenshot [target]`) that captures a single screenshot and exits — check `e.Args` handling in `App.xaml.cs` before changing startup flow, and keep this path working since it starts a fresh process each run (no cross-capture caching).

## Core Polling Loop

- `WindowWatcherService` runs the polling loop that detects window/foreground changes and idle state (via `UserActivityService`/`UserActivityHook`), and writes `WindowActivityEvent`s through `CsvDataService`.
- Process names are cached (`_processNamesCache`) to reduce CPU usage — do not remove this caching without profiling.

## Screenshot Capture

- `GraphicsCaptureService` wraps `Windows.Graphics.Capture` and aggressively caches expensive resources across captures within the same process: the `IDirect3DDevice`, the `IGraphicsCaptureItemInterop` factory, and per-monitor `GraphicsCaptureItem`s (keyed by HMONITOR, invalidated on `SystemEvents.DisplaySettingsChanged`). Window (HWND) capture items are **not** cached because HWNDs can be reused/become stale.
- The frame pool and capture session are recreated per capture rather than kept alive, to avoid a persistent capture border and constant GPU cost.
- `ScreenshotCaptureService` orchestrates capture + encoding (`FastPngEncoder`, `JpegImageEncoder`) into a `CapturedImage`. `CapturedImage` must stay `public` — the WPF `_wpftmp` temp build fails with CS0050 otherwise, since capture methods are public.
- `DataCleanupService` handles retention/cleanup of old logs and screenshots based on `AppSettings.LogRetentionDays` / `ScreenshotRetentionDays`, and archiving to `ScreenshotsArchivePath` after `ScreenshotsArchiveAfterDays`.
- See repo memory notes (`/memories/repo/screenshot-capture.md`) for detailed caching rationale and an isolated-build workaround for CS0050-style temp-build issues.

## Windows

- `MainWindow` is the tray-icon host (`Hardcodet.NotifyIcon.Wpf`); `SettingsWindow` edits a cloned `AppSettings` and only commits on save; `ViewEventsWindow` displays raw activity events for debugging.
- The Daemon opens `AnalyserWindow` and `ScreenshotViewerWindow` directly in-process via `ProjectReference` — do not spawn them as separate processes.

## Build Quirk

- When doing an isolated build of just the Daemon (e.g. to check for compile errors without touching the shared `bin/`), use `dotnet build WindowWatcher.Daemon/WindowWatcher.Daemon.csproj -p:OutputPath=bin_tmp/daemon/` then clean up with `Get-ChildItem -Directory -Recurse -Filter bin_tmp | Remove-Item -Recurse -Force`.
