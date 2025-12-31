---
applyTo: "WindowWatcher.Common/**"
---

# Common Library — WindowWatcher

These instructions apply to all files under `WindowWatcher.Common/`.

---

## Placement Rules

- `WindowWatcher.Common` must not reference `PresentationFramework`, `WindowsBase`, or any other WPF/window-specific assembly — it is shared by all three UI executables.
- Data models go in `Models/`, stateful/IO services in `Services/`, and static helper/extension logic in `Utilities/`.

## Models

- `AppSettings` holds all user-configurable settings (paths, retention periods, capture options). Every property must have a sensible default so the app works on first run with no settings file.
- When adding a property to `AppSettings`, also update its `Clone()` method — settings windows clone the current settings to allow cancel/rollback.
- `WindowActivityEvent` / `WindowActivityEventType` model a single activity log entry. Serialisation to disk is via the custom `ToCsvString()` method, not a generic CSV/JSON library — keep new fields consistent with the existing comma-separated, quote-escaped format.

## Services

- `SettingsService.LoadSettings()` caches the loaded `AppSettings` in-memory (`_cachedSettings`) after first load and resolves relative paths (`ScreenshotsPath`, `DataDirectory`, `ScreenshotsArchivePath`) against `%APPDATA%\WindowWatcher\`. Any new path-type setting should get the same relative-path resolution treatment.
- `CsvDataService` reads/writes the activity-log CSV files; `CsvMigrationUtility` handles one-time migration of legacy log formats — extend this rather than adding ad-hoc migration code elsewhere.
- `LoggingService` implements `ILogger` and writes to `%APPDATA%\WindowWatcher\Logs`. It is constructed with a `SettingsService` so the configured `LogLevel` is respected. Inject `LoggingService` (or `ILogger`) into new services instead of writing directly to files or the console.
- `WindowsApiService` centralises raw Win32 interop calls used across projects — add new P/Invoke declarations here rather than duplicating them in the Daemon/Analyser/ScreenshotViewer projects.

## JSON / Serialisation

- Use `System.Text.Json` for settings serialisation (`SettingsService`) — do not introduce `Newtonsoft.Json`.
