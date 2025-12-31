---
applyTo: "WindowWatcher.Analyser/**"
---

# Analyser — WindowWatcher

These instructions apply to all files under `WindowWatcher.Analyser/`.

---

## Purpose

- The Analyser is a Gantt-style timeline viewer for the activity CSV logs written by the Daemon. It reads data via `WindowWatcher.Common.Services.CsvDataService` and `SettingsService` — do not add a second CSV-reading code path here.

## ViewModel

- `AnalyserViewModel` loads all events for the selected date range (`SelectedDate` +/- `_hoursVisible`), optionally filtered by `SelectedProcess`, into `FilteredEvents`.
- `Rows` is the ordered list of row labels shown in the timeline (one per process, or per process+window-title when a specific process is selected), sorted by total duration descending, with `"Idle"` always pinned to the top. When changing row-grouping/sorting logic, keep the `"Idle"` pinning behavior intact.
- Commands (`PreviousDayCommand`, `NextDayCommand`, `ScrollLeftCommand`/`ScrollRightCommand`, `ZoomInCommand`/`ZoomOutCommand`, `FullDayCommand`) all mutate `_hoursVisible`/`SelectedDate` and then call `UpdateTimeWindow()` + `LoadEvents()` — follow this pattern for any new navigation command rather than mutating `StartTime`/`EndTime` directly.
- Zoom is clamped between 1 and 48 hours visible (`ZoomIn`/`ZoomOut`).

## TimelineCanvas

- `Controls/TimelineCanvas.cs` is a custom-drawn (non-databound-per-element) Gantt chart control. It draws directly rather than using a `DataGrid`/`ItemsControl` per row for performance with many events — keep new visualisations in this control consistent with that direct-draw approach rather than introducing per-item WPF controls for large datasets.
