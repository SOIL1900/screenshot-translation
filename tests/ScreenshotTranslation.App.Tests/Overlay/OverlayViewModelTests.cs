using ScreenshotTranslation.App.Overlay;
using ScreenshotTranslation.Core.Configuration;
using ScreenshotTranslation.Core.Geometry;
using ScreenshotTranslation.Core.Translation;
using ScreenshotTranslation.Infrastructure.Windows;

namespace ScreenshotTranslation.App.Tests.Overlay;

public sealed class OverlayViewModelTests
{
    [Fact]
    public async Task Releasing_a_valid_selection_starts_one_translation()
    {
        var fixture = OverlayViewModelFixture.Create();

        fixture.ViewModel.BeginSelection(new PixelPoint(100, 100));
        fixture.ViewModel.UpdatePointer(new PixelPoint(400, 250));
        await fixture.ViewModel.CompletePointerActionAsync();

        Assert.Equal(new PixelRect(100, 100, 300, 150), fixture.ViewModel.Selection);
        Assert.Equal(1, fixture.TranslationCoordinator.ScreenshotCallCount);
    }

    [Fact]
    public async Task Drag_updates_do_not_translate_until_mouse_release()
    {
        var fixture = OverlayViewModelFixture.Create(
            existingSelection: new PixelRect(100, 100, 300, 150));

        fixture.ViewModel.BeginResize(ResizeHandle.Right, new PixelPoint(400, 175));
        fixture.ViewModel.UpdatePointer(new PixelPoint(450, 175));
        fixture.ViewModel.UpdatePointer(new PixelPoint(500, 175));
        Assert.Equal(0, fixture.TranslationCoordinator.ScreenshotCallCount);

        await fixture.ViewModel.CompletePointerActionAsync();
        Assert.Equal(1, fixture.TranslationCoordinator.ScreenshotCallCount);
    }

    [Fact]
    public async Task Small_selection_closes_without_translation()
    {
        var fixture = OverlayViewModelFixture.Create();

        fixture.ViewModel.BeginSelection(new PixelPoint(10, 10));
        fixture.ViewModel.UpdatePointer(new PixelPoint(20, 20));
        await fixture.ViewModel.CompletePointerActionAsync();

        Assert.Null(fixture.ViewModel.Selection);
        Assert.Equal(1, fixture.CloseRequestCount);
        Assert.Equal(0, fixture.TranslationCoordinator.ScreenshotCallCount);
    }

    [Fact]
    public async Task Moving_selection_translates_once_after_release()
    {
        var fixture = OverlayViewModelFixture.Create(
            existingSelection: new PixelRect(100, 100, 300, 150));

        fixture.ViewModel.BeginMove(new PixelPoint(150, 150));
        fixture.ViewModel.UpdatePointer(new PixelPoint(200, 180));

        Assert.Equal(new PixelRect(150, 130, 300, 150), fixture.ViewModel.Selection);
        Assert.Equal(0, fixture.TranslationCoordinator.ScreenshotCallCount);

        await fixture.ViewModel.CompletePointerActionAsync();
        Assert.Equal(1, fixture.TranslationCoordinator.ScreenshotCallCount);
    }

    [Theory]
    [MemberData(nameof(ResizeCases))]
    public async Task Every_resize_handle_updates_geometry_and_translates_only_on_release(
        ResizeHandle handle,
        PixelPoint start,
        PixelPoint end,
        PixelRect expected)
    {
        var fixture = OverlayViewModelFixture.Create(
            existingSelection: new PixelRect(100, 100, 300, 200));

        fixture.ViewModel.BeginResize(handle, start);
        fixture.ViewModel.UpdatePointer(end);

        Assert.Equal(expected, fixture.ViewModel.Selection);
        Assert.Equal(0, fixture.TranslationCoordinator.ScreenshotCallCount);

        await fixture.ViewModel.CompletePointerActionAsync();
        Assert.Equal(1, fixture.TranslationCoordinator.ScreenshotCallCount);
    }

    [Fact]
    public async Task Stale_screenshot_result_does_not_replace_latest_result()
    {
        var fixture = OverlayViewModelFixture.Create(holdScreenshotTranslations: true);

        fixture.ViewModel.BeginSelection(new PixelPoint(10, 10));
        fixture.ViewModel.UpdatePointer(new PixelPoint(210, 110));
        var firstCompletion = fixture.ViewModel.CompletePointerActionAsync();

        fixture.ViewModel.BeginSelection(new PixelPoint(300, 200));
        fixture.ViewModel.UpdatePointer(new PixelPoint(600, 400));
        var secondCompletion = fixture.ViewModel.CompletePointerActionAsync();

        fixture.TranslationCoordinator.CompleteScreenshot(
            1,
            ScreenshotResult("Japanese", "ja", "最新译文"));
        await secondCompletion;
        fixture.TranslationCoordinator.CompleteScreenshot(
            0,
            ScreenshotResult("English", "en", "过期译文"));
        await firstCompletion;

        Assert.Equal("最新译文", fixture.ViewModel.ScreenshotTranslation);
        Assert.Equal("ja", fixture.ViewModel.DetectedSourceLanguageCode);
    }

    [Fact]
    public async Task Stale_screenshot_failure_is_ignored()
    {
        var fixture = OverlayViewModelFixture.Create(holdScreenshotTranslations: true);

        fixture.ViewModel.BeginSelection(new PixelPoint(10, 10));
        fixture.ViewModel.UpdatePointer(new PixelPoint(210, 110));
        var firstCompletion = fixture.ViewModel.CompletePointerActionAsync();

        fixture.ViewModel.BeginSelection(new PixelPoint(300, 200));
        fixture.ViewModel.UpdatePointer(new PixelPoint(600, 400));
        var secondCompletion = fixture.ViewModel.CompletePointerActionAsync();

        fixture.TranslationCoordinator.CompleteScreenshot(
            1,
            ScreenshotResult("Japanese", "ja", "latest translation"));
        await secondCompletion;
        fixture.TranslationCoordinator.FailScreenshot(
            0,
            new TranslationClientException(TranslationErrorCode.Network, "stale failure"));
        await firstCompletion;

        Assert.Equal("latest translation", fixture.ViewModel.ScreenshotTranslation);
        Assert.Equal(OverlayTranslationState.Success, fixture.ViewModel.ScreenshotState);
    }

    [Fact]
    public void Escape_outside_click_and_right_click_cancel_and_close()
    {
        var escapeFixture = OverlayViewModelFixture.Create();
        escapeFixture.ViewModel.HandleEscape();
        Assert.Equal(1, escapeFixture.CloseRequestCount);
        Assert.Equal(1, escapeFixture.TranslationCoordinator.CancelCount);

        var outsideFixture = OverlayViewModelFixture.Create();
        outsideFixture.ViewModel.HandleOutsideClick();
        Assert.Equal(1, outsideFixture.CloseRequestCount);
        Assert.Equal(1, outsideFixture.TranslationCoordinator.CancelCount);

        var rightClickFixture = OverlayViewModelFixture.Create();
        rightClickFixture.ViewModel.HandleRightClick();
        Assert.Equal(1, rightClickFixture.CloseRequestCount);
        Assert.Equal(1, rightClickFixture.TranslationCoordinator.CancelCount);
    }

    [Fact]
    public async Task No_text_result_keeps_selection_and_retry_submits_same_crop()
    {
        var fixture = OverlayViewModelFixture.Create(
            screenshotResult: new ScreenshotTranslationResult(
                TranslationResultStatus.NoText,
                string.Empty,
                string.Empty,
                string.Empty));

        await fixture.SelectValidAreaAsync();

        Assert.Equal(OverlayTranslationState.NoText, fixture.ViewModel.ScreenshotState);
        Assert.Contains("未识别到", fixture.ViewModel.StatusMessage);
        Assert.NotNull(fixture.ViewModel.Selection);

        await fixture.ViewModel.RetryScreenshotAsync();
        Assert.Equal(2, fixture.TranslationCoordinator.ScreenshotCallCount);
        Assert.Equal(
            fixture.TranslationCoordinator.ScreenshotCalls[0].PngBytes,
            fixture.TranslationCoordinator.ScreenshotCalls[1].PngBytes);
    }

    [Fact]
    public async Task Reply_target_defaults_to_detected_source_language()
    {
        var fixture = OverlayViewModelFixture.Create(
            screenshotResult: ScreenshotResult("Japanese", "ja", "こんにちは"));
        await fixture.SelectValidAreaAsync();
        fixture.ViewModel.ReplyInput = "你好";

        await fixture.ViewModel.TranslateReplyAsync();

        Assert.Equal("ja", fixture.ViewModel.ReplyTargetLanguage);
        Assert.Equal("ja", fixture.TranslationCoordinator.ReplyCalls.Single().TargetLanguageCode);
    }

    [Fact]
    public async Task Editing_reply_while_translation_is_in_flight_suppresses_stale_result()
    {
        var fixture = OverlayViewModelFixture.Create(holdReplyTranslations: true);
        await fixture.SelectValidAreaAsync();
        fixture.ViewModel.ReplyInput = "first reply";

        var translation = fixture.ViewModel.TranslateReplyAsync();
        fixture.ViewModel.ReplyInput = "updated reply";
        fixture.TranslationCoordinator.CompleteReply(
            0,
            new ReplyTranslationResult("en", "stale translation"));
        await translation;

        Assert.Null(fixture.ViewModel.ReplyTranslation);
        Assert.Equal(OverlayTranslationState.Idle, fixture.ViewModel.ReplyState);
        Assert.Equal(1, fixture.TranslationCoordinator.CancelCount);
    }

    [Fact]
    public async Task Screenshot_retranslation_supersedes_in_flight_reply_without_stuck_loading()
    {
        var fixture = OverlayViewModelFixture.Create(holdReplyTranslations: true);
        await fixture.SelectValidAreaAsync();
        fixture.ViewModel.ReplyInput = "reply";
        var replyTranslation = fixture.ViewModel.TranslateReplyAsync();

        await fixture.ViewModel.ChangeScreenshotTargetLanguageAsync("en");
        fixture.TranslationCoordinator.CompleteReply(
            0,
            new ReplyTranslationResult("en", "stale translation"));
        await replyTranslation;

        Assert.Equal(OverlayTranslationState.Success, fixture.ViewModel.ScreenshotState);
        Assert.False(fixture.ViewModel.IsScreenshotLoadingVisible);
        Assert.Equal(OverlayTranslationState.Idle, fixture.ViewModel.ReplyState);
        Assert.Null(fixture.ViewModel.ReplyTranslation);
    }

    [Fact]
    public async Task Missing_detected_source_requires_manual_reply_target()
    {
        var fixture = OverlayViewModelFixture.Create(
            screenshotResult: ScreenshotResult("", "", "fallback translation"));

        await fixture.SelectValidAreaAsync();

        Assert.Null(fixture.ViewModel.ReplyTargetLanguage);
        Assert.False(fixture.ViewModel.CanTranslateReply);
        Assert.Contains("选择回复目标语言", fixture.ViewModel.ReplyStatusMessage);
    }

    [Fact]
    public async Task Changing_screenshot_target_language_retranslates_current_crop_once()
    {
        var fixture = OverlayViewModelFixture.Create();
        await fixture.SelectValidAreaAsync();

        await fixture.ViewModel.ChangeScreenshotTargetLanguageAsync("en");

        Assert.Equal(2, fixture.TranslationCoordinator.ScreenshotCallCount);
        Assert.Equal("en", fixture.TranslationCoordinator.ScreenshotCalls[1].TargetLanguageCode);
        Assert.Equal(
            fixture.TranslationCoordinator.ScreenshotCalls[0].PngBytes,
            fixture.TranslationCoordinator.ScreenshotCalls[1].PngBytes);
    }

    [Fact]
    public async Task Loading_feedback_appears_only_after_300_millisecond_delay()
    {
        var fixture = OverlayViewModelFixture.Create(
            holdScreenshotTranslations: true,
            holdLoadingDelay: true);

        var selectionCompletion = fixture.SelectValidAreaAsync();
        Assert.False(fixture.ViewModel.IsScreenshotLoadingVisible);

        fixture.Delay.Complete();
        await WaitUntilAsync(() => fixture.ViewModel.IsScreenshotLoadingVisible);

        fixture.TranslationCoordinator.CompleteScreenshot(
            0,
            ScreenshotResult("English", "en", "译文"));
        await selectionCompletion;
        Assert.False(fixture.ViewModel.IsScreenshotLoadingVisible);
    }

    [Fact]
    public async Task Copy_writes_visible_translation_and_requests_close()
    {
        var fixture = OverlayViewModelFixture.Create(
            screenshotResult: ScreenshotResult("English", "en", "精确可见译文"));
        await fixture.SelectValidAreaAsync();

        fixture.ViewModel.CopyScreenshotTranslation();

        Assert.Equal("精确可见译文", fixture.Clipboard.Writes.Single());
        Assert.Equal(1, fixture.CloseRequestCount);
    }

    [Fact]
    public async Task Clipboard_failure_keeps_overlay_open_with_recovery_message()
    {
        var fixture = OverlayViewModelFixture.Create(
            screenshotResult: ScreenshotResult("English", "en", "译文"),
            clipboardFailure: new InvalidOperationException("Clipboard busy"));
        await fixture.SelectValidAreaAsync();

        fixture.ViewModel.CopyScreenshotTranslation();

        Assert.Equal(0, fixture.CloseRequestCount);
        Assert.Contains("重试", fixture.ViewModel.ClipboardError);
    }

    [Fact]
    public async Task Crop_failure_keeps_selection_and_can_retry()
    {
        var fixture = OverlayViewModelFixture.Create(
            cropFailure: new InvalidOperationException("Invalid PNG crop"));

        await fixture.SelectValidAreaAsync();

        Assert.Equal(OverlayTranslationState.Error, fixture.ViewModel.ScreenshotState);
        Assert.True(fixture.ViewModel.CanRetryScreenshot);
        Assert.NotNull(fixture.ViewModel.Selection);
        Assert.Equal(0, fixture.CloseRequestCount);
        Assert.Equal(0, fixture.TranslationCoordinator.ScreenshotCallCount);

        await fixture.ViewModel.RetryScreenshotAsync();

        Assert.Equal(OverlayTranslationState.Success, fixture.ViewModel.ScreenshotState);
        Assert.Equal(1, fixture.TranslationCoordinator.ScreenshotCallCount);
    }

    [Fact]
    public void Coordinate_mapper_keeps_150_percent_dpi_mapping_monitor_local()
    {
        var mapper = new OverlayCoordinateMapper(144, 144);

        Assert.Equal(new PixelPoint(150, 75), mapper.ToPhysical(new System.Windows.Point(100, 50)));
        Assert.Equal(new System.Windows.Rect(100, 50, 200, 100),
            mapper.ToDip(new PixelRect(150, 75, 300, 150)));
        Assert.Equal(300, mapper.DipLengthToPhysicalX(200));
        Assert.Equal(150, mapper.DipLengthToPhysicalY(100));
    }

    [Fact]
    public void View_model_rejects_global_desktop_screen_bounds()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            OverlayViewModelFixture.Create(
                screenBounds: new PixelRect(-1920, 100, 1920, 1080)));

        Assert.Equal("screenBounds", exception.ParamName);
    }

    [Fact]
    public async Task Secondary_monitor_global_origin_never_enters_selection_or_crop_coordinates()
    {
        var monitor = new MonitorBounds(
            (nint)2,
            new PixelRect(-1920, 100, 1920, 1080));
        var frameBounds = new PixelRect(
            0,
            0,
            monitor.PhysicalBounds.Width,
            monitor.PhysicalBounds.Height);
        var fixture = OverlayViewModelFixture.Create(
            screenBounds: frameBounds);

        fixture.ViewModel.BeginSelection(new PixelPoint(120, 100));
        fixture.ViewModel.UpdatePointer(new PixelPoint(420, 250));
        await fixture.ViewModel.CompletePointerActionAsync();

        Assert.Equal(frameBounds, fixture.ViewModel.ScreenBounds);
        Assert.Equal(new PixelRect(120, 100, 300, 150), fixture.ViewModel.Selection);
        Assert.Equal(
            new PixelRect(120, 100, 300, 150),
            fixture.CropService.CropRectangles.Single());
    }

    public static TheoryData<ResizeHandle, PixelPoint, PixelPoint, PixelRect> ResizeCases => new()
    {
        { ResizeHandle.TopLeft, new PixelPoint(100, 100), new PixelPoint(120, 130), new PixelRect(120, 130, 280, 170) },
        { ResizeHandle.Top, new PixelPoint(250, 100), new PixelPoint(250, 130), new PixelRect(100, 130, 300, 170) },
        { ResizeHandle.TopRight, new PixelPoint(400, 100), new PixelPoint(420, 130), new PixelRect(100, 130, 320, 170) },
        { ResizeHandle.Right, new PixelPoint(400, 200), new PixelPoint(420, 200), new PixelRect(100, 100, 320, 200) },
        { ResizeHandle.BottomRight, new PixelPoint(400, 300), new PixelPoint(420, 330), new PixelRect(100, 100, 320, 230) },
        { ResizeHandle.Bottom, new PixelPoint(250, 300), new PixelPoint(250, 330), new PixelRect(100, 100, 300, 230) },
        { ResizeHandle.BottomLeft, new PixelPoint(100, 300), new PixelPoint(120, 330), new PixelRect(120, 100, 280, 230) },
        { ResizeHandle.Left, new PixelPoint(100, 200), new PixelPoint(120, 200), new PixelRect(120, 100, 280, 200) }
    };

    private static ScreenshotTranslationResult ScreenshotResult(
        string sourceLanguage,
        string sourceLanguageCode,
        string translation) =>
        new(TranslationResultStatus.Ok, sourceLanguage, sourceLanguageCode, translation);

    private static async Task WaitUntilAsync(Func<bool> predicate)
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!predicate())
        {
            await Task.Delay(10, cancellation.Token);
        }
    }

    private sealed class OverlayViewModelFixture
    {
        private OverlayViewModelFixture(
            OverlayViewModel viewModel,
            FakeOverlayTranslationCoordinator translationCoordinator,
            FakePngCropService cropService,
            FakeOverlayClipboardService clipboard,
            FakeOverlayDelay delay)
        {
            ViewModel = viewModel;
            TranslationCoordinator = translationCoordinator;
            CropService = cropService;
            Clipboard = clipboard;
            Delay = delay;
            viewModel.CloseRequested += (_, _) => CloseRequestCount++;
        }

        public OverlayViewModel ViewModel { get; }

        public FakeOverlayTranslationCoordinator TranslationCoordinator { get; }

        public FakePngCropService CropService { get; }

        public FakeOverlayClipboardService Clipboard { get; }

        public FakeOverlayDelay Delay { get; }

        public int CloseRequestCount { get; private set; }

        public static OverlayViewModelFixture Create(
            PixelRect? existingSelection = null,
            ScreenshotTranslationResult? screenshotResult = null,
            bool holdScreenshotTranslations = false,
            bool holdReplyTranslations = false,
            bool holdLoadingDelay = false,
            Exception? clipboardFailure = null,
            Exception? cropFailure = null,
            PixelRect? screenBounds = null)
        {
            var coordinator = new FakeOverlayTranslationCoordinator(
                holdScreenshotTranslations,
                holdReplyTranslations,
                screenshotResult ?? ScreenshotResult("English", "en", "你好"));
            var crop = new FakePngCropService(cropFailure);
            var clipboard = new FakeOverlayClipboardService(clipboardFailure);
            var delay = new FakeOverlayDelay(holdLoadingDelay);
            var viewModel = new OverlayViewModel(
                frozenPng: [0x89, 0x50, 0x4E, 0x47],
                screenBounds: screenBounds ?? new PixelRect(0, 0, 1000, 800),
                existingSelection,
                screenshotTargetLanguage: "zh-CN",
                AppSettings.CreateDefault().Model with { ApiKey = "sk-test" },
                crop,
                coordinator,
                clipboard,
                delay);
            return new OverlayViewModelFixture(viewModel, coordinator, crop, clipboard, delay);
        }

        public async Task SelectValidAreaAsync()
        {
            ViewModel.BeginSelection(new PixelPoint(100, 100));
            ViewModel.UpdatePointer(new PixelPoint(400, 250));
            await ViewModel.CompletePointerActionAsync();
        }
    }

    private sealed class FakePngCropService(Exception? failure) : IPngCropService
    {
        private Exception? _failure = failure;

        public List<PixelRect> CropRectangles { get; } = [];

        public byte[] Crop(byte[] capturedPng, PixelRect cropRectangle)
        {
            CropRectangles.Add(cropRectangle);
            if (_failure is { } failure)
            {
                _failure = null;
                throw failure;
            }

            return
            [
                (byte)(cropRectangle.X & 0xFF),
                (byte)(cropRectangle.Y & 0xFF),
                (byte)(cropRectangle.Width & 0xFF),
                (byte)(cropRectangle.Height & 0xFF)
            ];
        }
    }

    private sealed class FakeOverlayTranslationCoordinator(
        bool holdScreenshotTranslations,
        bool holdReplyTranslations,
        ScreenshotTranslationResult defaultScreenshotResult) : IOverlayTranslationCoordinator
    {
        private readonly List<TaskCompletionSource<ScreenshotTranslationResult?>> _pendingScreenshots = [];
        private readonly List<TaskCompletionSource<ReplyTranslationResult?>> _pendingReplies = [];

        public List<ScreenshotCall> ScreenshotCalls { get; } = [];

        public List<ReplyCall> ReplyCalls { get; } = [];

        public int ScreenshotCallCount => ScreenshotCalls.Count;

        public int CancelCount { get; private set; }

        public Task<ScreenshotTranslationResult?> TranslateScreenshotAsync(
            ReadOnlyMemory<byte> pngBytes,
            string targetLanguageCode,
            ModelSettings settings,
            CancellationToken cancellationToken)
        {
            ScreenshotCalls.Add(new ScreenshotCall(pngBytes.ToArray(), targetLanguageCode));
            if (!holdScreenshotTranslations)
            {
                return Task.FromResult<ScreenshotTranslationResult?>(defaultScreenshotResult);
            }

            var completion = new TaskCompletionSource<ScreenshotTranslationResult?>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            _pendingScreenshots.Add(completion);
            return completion.Task;
        }

        public Task<ReplyTranslationResult?> TranslateReplyAsync(
            string input,
            string targetLanguageCode,
            ModelSettings settings,
            CancellationToken cancellationToken)
        {
            ReplyCalls.Add(new ReplyCall(input, targetLanguageCode));
            if (holdReplyTranslations)
            {
                var completion = new TaskCompletionSource<ReplyTranslationResult?>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                _pendingReplies.Add(completion);
                return completion.Task;
            }

            return Task.FromResult<ReplyTranslationResult?>(
                new ReplyTranslationResult(targetLanguageCode, $"translated:{input}"));
        }

        public void CompleteScreenshot(int index, ScreenshotTranslationResult result) =>
            _pendingScreenshots[index].TrySetResult(result);

        public void FailScreenshot(int index, Exception exception) =>
            _pendingScreenshots[index].TrySetException(exception);

        public void CompleteReply(int index, ReplyTranslationResult result) =>
            _pendingReplies[index].TrySetResult(result);

        public void Cancel() => CancelCount++;
    }

    private sealed class FakeOverlayClipboardService(Exception? failure) : IOverlayClipboardService
    {
        public List<string> Writes { get; } = [];

        public void SetText(string text)
        {
            if (failure is not null)
            {
                throw failure;
            }

            Writes.Add(text);
        }
    }

    private sealed class FakeOverlayDelay(bool hold) : IOverlayDelay
    {
        private readonly TaskCompletionSource _completion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void Complete() => _completion.TrySetResult();

        public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken) =>
            hold ? _completion.Task : Task.CompletedTask;
    }

    private sealed record ScreenshotCall(byte[] PngBytes, string TargetLanguageCode);

    private sealed record ReplyCall(string Input, string TargetLanguageCode);
}
