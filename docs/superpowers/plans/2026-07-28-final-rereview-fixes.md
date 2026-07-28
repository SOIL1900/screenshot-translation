# Final Re-review Fixes Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Move screenshot normalization/Base64 work off the WPF caller path with cancellation, and size result panels from monitor work-area dimensions.

**Architecture:** `OpenAiTranslationClient` receives a small async request-image boundary; production uses a cancellable `Task.Run` PNG implementation while tests inject a held fake. A pure App layout helper computes work-area-constrained DIP size and physical placement, leaving the window responsible only for applying the result.

**Tech Stack:** .NET 8, C# 12, WPF, System.Drawing, xUnit, WiX 5.

---

### Task 1: Async Cancellable Request Images

**Files:**
- Create: `src/ScreenshotTranslation.Infrastructure/Translation/IRequestImageNormalizer.cs`
- Modify: `src/ScreenshotTranslation.Infrastructure/Translation/PngRequestImageNormalizer.cs`
- Modify: `src/ScreenshotTranslation.Infrastructure/Translation/OpenAiTranslationClient.cs`
- Modify: `src/ScreenshotTranslation.Infrastructure/Translation/OpenAiRequestFactory.cs`
- Modify: `tests/ScreenshotTranslation.Infrastructure.Tests/Translation/OpenAiTranslationClientTests.cs`
- Modify: `tests/ScreenshotTranslation.Infrastructure.Tests/Translation/OpenAiRequestFactoryTests.cs`

- [ ] Add held-fake RED tests for immediate yielding, caller/coordinator cancellation, and zero HTTP sends before normalization completes.
- [ ] Inject `IRequestImageNormalizer` through an internal test constructor while retaining the public production constructor.
- [ ] Run real decode/render/Base64 in `Task.Run`, checking cancellation before decode, render, Base64, and each iterative resize.
- [ ] Await normalized data URL before request creation/network and preserve caller `OperationCanceledException`.
- [ ] Run only normalizer/client/cancellation tests and retain limit/no-upscale coverage.

### Task 2: Work-area-constrained Panel Size

**Files:**
- Create: `src/ScreenshotTranslation.App/Overlay/OverlayPanelLayout.cs`
- Modify: `src/ScreenshotTranslation.App/Overlay/CaptureOverlayWindow.xaml.cs`
- Create: `tests/ScreenshotTranslation.App.Tests/Overlay/OverlayPanelLayoutTests.cs`

- [ ] Add a RED seam test for 144-DPI local work area narrower than 420 DIPs with side/top taskbar offsets.
- [ ] Compute maximum DIP width/height from `FrameLocalWorkArea`, convert to physical size, and place entirely inside the same work area.
- [ ] Apply the pure result in `CaptureOverlayWindow` and run only panel-layout plus existing overlay cancellation tests.

### Task 3: Focused Release and Artifacts

**Files:**
- Create: `.superpowers/sdd/final-fix-report-2.md`

- [ ] Build App and Infrastructure in Release and run no full solution suite.
- [ ] Publish self-contained win-x64 once and rebuild MSI once.
- [ ] Record hashes, focused RED/GREEN, boundaries, process state, and commits.
