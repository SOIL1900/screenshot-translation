# Final Branch Review Fixes Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Resolve the five Important final-review defects plus the diagnostic and About-screen minors without live-provider, blocking-GUI, or installer side effects.

**Architecture:** Keep capture geometry in physical pixels and frame-local coordinates, with DPI conversion owned by the WPF overlay. Validate persisted settings at the storage boundary, keep fallback language metadata explicitly absent, and normalize only screenshot PNG request payloads before Base64 encoding. Preserve content-free diagnostics and avoid repository links until a real remote exists.

**Tech Stack:** .NET 8, C# 12, WPF/Win32, System.Drawing PNG processing, xUnit, WiX 5.

---

### Task 1: Mixed-DPI Overlay Refresh

**Files:**
- Modify: `src/ScreenshotTranslation.App/Overlay/OverlayCoordinateMapper.cs`
- Modify: `src/ScreenshotTranslation.App/Overlay/CaptureOverlayWindow.xaml.cs`
- Test: `tests/ScreenshotTranslation.App.Tests/Overlay/OverlayViewModelTests.cs`

- [ ] Add an internal mapper-state seam whose `Refresh(uint dpiX, uint dpiY)` replaces the current mapper.
- [ ] Add a focused RED test that refreshes from 96 DPI to asymmetric target DPI and asserts new physical coordinates.
- [ ] Position the HWND at the monitor's global physical bounds before calling `GetDpiForWindow` for the initial mapper.
- [ ] Hook `WM_DPICHANGED`, refresh the mapper from the message DPI, propagate it to `SelectionCanvas`, and recalculate the panel.
- [ ] Run only `dotnet test tests/ScreenshotTranslation.App.Tests/ScreenshotTranslation.App.Tests.csproj -c Release --no-restore --filter "FullyQualifiedName~Coordinate_mapper"` and record RED/GREEN.

### Task 2: Monitor Work-Area Panel Placement

**Files:**
- Modify: `src/ScreenshotTranslation.Infrastructure/Windows/ScreenCaptureContracts.cs`
- Modify: `src/ScreenshotTranslation.Infrastructure/Windows/MonitorService.cs`
- Modify: `src/ScreenshotTranslation.App/Overlay/CaptureOverlayWindow.xaml.cs`
- Modify: affected monitor construction in tests
- Test: `tests/ScreenshotTranslation.Core.Tests/Geometry/ResultPanelPlacementTests.cs`
- Test: `tests/ScreenshotTranslation.Infrastructure.Tests/Windows/MonitorBoundsTests.cs`

- [ ] Extend `MonitorBounds` with `PhysicalWorkArea` and a frame-local projection computed by subtracting the full monitor origin.
- [ ] Populate both monitor and work rectangles from `MONITORINFO`; keep capture and HWND sizing on `PhysicalBounds`.
- [ ] Pass only the local work area to `ResultPanelPlacement.Place`.
- [ ] Add bottom-taskbar, side-taskbar, and negative-origin projection/placement tests.
- [ ] Run only the new/affected work-area tests and record RED/GREEN.

### Task 3: Absent Fallback Source Language

**Files:**
- Modify: `src/ScreenshotTranslation.Core/Translation/TranslationContracts.cs`
- Modify: `src/ScreenshotTranslation.Infrastructure/Translation/OpenAiResponseParser.cs`
- Modify: `src/ScreenshotTranslation.App/Overlay/OverlayViewModel.cs`
- Test: `tests/ScreenshotTranslation.Infrastructure.Tests/Translation/OpenAiResponseParserTests.cs`
- Test: `tests/ScreenshotTranslation.App.Tests/Overlay/OverlayViewModelTests.cs`

- [ ] Make screenshot source-language metadata nullable and return null metadata for plain-text fallback.
- [ ] Normalize both display/code metadata to null in the overlay and leave the reply target unset.
- [ ] Add a parser-to-overlay test proving fallback content requires manual reply target selection, then succeeds with that manual target.
- [ ] Run only the fallback parser/overlay tests and record RED/GREEN.

### Task 4: Persisted Settings Integrity

**Files:**
- Modify: `src/ScreenshotTranslation.Core/Configuration/SettingsValidator.cs`
- Modify: `src/ScreenshotTranslation.Infrastructure/Configuration/JsonSettingsStore.cs`
- Test: `tests/ScreenshotTranslation.Core.Tests/Configuration/SettingsValidatorTests.cs`
- Test: `tests/ScreenshotTranslation.Infrastructure.Tests/Configuration/JsonSettingsStoreTests.cs`

- [ ] Add persisted-settings validation that checks nested records, hotkey, URL, model, numeric ranges, enum/language invariants, and extra-JSON object syntax while allowing an empty API key.
- [ ] Treat deserialized null or invalid settings as `JsonException`, back up the original, and return defaults.
- [ ] Add cases for `{}`, null General/Model, invalid numbers, invalid extra JSON, and a valid empty-key file.
- [ ] Run only settings validator/store tests and record RED/GREEN.

### Task 5: Screenshot PNG Normalization

**Files:**
- Create: `src/ScreenshotTranslation.Infrastructure/Translation/PngRequestImageNormalizer.cs`
- Modify: `src/ScreenshotTranslation.Infrastructure/Translation/OpenAiRequestFactory.cs`
- Create: `tests/ScreenshotTranslation.Infrastructure.Tests/Translation/PngRequestImageNormalizerTests.cs`
- Modify: request/client tests that currently pass PNG signature fragments
- Modify: `README.md`

- [ ] Define `MaxLongEdgePixels = 2048` and `MaxEncodedPngBytes = 8 * 1024 * 1024`.
- [ ] Return valid small PNG bytes unchanged; otherwise proportionally render PNG output without upscaling and iteratively reduce while output exceeds 8 MiB.
- [ ] Normalize immediately before request Base64 encoding.
- [ ] Test unchanged small PNG, 3840x2160 proportional resize, deterministic high-entropy payload cap, and decodable PNG output.
- [ ] Update request/client fixtures to valid PNGs and document both limits in README.
- [ ] Run only normalizer/request/client screenshot tests and record RED/GREEN.

### Task 6: Diagnostic Categories and About Accuracy

**Files:**
- Modify: `src/ScreenshotTranslation.Infrastructure/Diagnostics/FileDiagnosticLog.cs`
- Modify: `tests/ScreenshotTranslation.Infrastructure.Tests/Diagnostics/FileDiagnosticLogTests.cs`
- Modify: `src/ScreenshotTranslation.App/Settings/AboutView.xaml`

- [ ] Add the three App-emitted content-free event names to the fixed allow-list.
- [ ] Test exact schema and message redaction for every allowed App event while retaining unrecognized-event sanitization.
- [ ] Replace the false shipped-dependency-list statement with an accessible MIT/open-source statement; do not add a repository URL because `git remote -v` is empty.
- [ ] Run only diagnostic tests and an App Release/XAML build.

### Task 7: One Final Verification and Packaging Pass

**Files:**
- Create: `.superpowers/sdd/final-fix-report.md`

- [ ] Run one clean Release build/test sequence for the cross-cutting wave, then `git diff --check`.
- [ ] Run the repository secret scan once and classify only fixture/documentation matches.
- [ ] Publish self-contained `win-x64` once and rebuild the MSI once.
- [ ] Record final EXE/DLL/MSI SHA-256 hashes, file counts, commands, focused/full results, and manual boundaries.
- [ ] Confirm no `ScreenshotTranslation`, `testhost`, or `vstest` process remains, commit the fixes, and write the report.
