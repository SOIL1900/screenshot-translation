using ScreenshotTranslation.Core.Configuration;
using ScreenshotTranslation.Core.Translation;

namespace ScreenshotTranslation.Core.Tests.Translation;

public sealed class TranslationCoordinatorTests
{
    [Fact]
    public async Task New_screenshot_request_cancels_and_supersedes_the_old_request()
    {
        var client = new ControllableTranslationClient();
        var coordinator = new TranslationCoordinator(client);

        var first = coordinator.TranslateScreenshotAsync(new byte[] { 1 }, "zh-CN", Settings(), CancellationToken.None);
        var second = coordinator.TranslateScreenshotAsync(new byte[] { 2 }, "zh-CN", Settings(), CancellationToken.None);

        Assert.True(client.WasScreenshotCanceled(1));

        client.CompleteScreenshot(1, ScreenshotResult("French", "fr", "old result"));
        client.CompleteScreenshot(2, ScreenshotResult("English", "en", "new result"));

        Assert.Null(await first);
        Assert.Equal("new result", (await second)!.Translation);
    }

    [Fact]
    public async Task Cancel_cancels_and_invalidates_the_active_request()
    {
        var client = new ControllableTranslationClient();
        var coordinator = new TranslationCoordinator(client);

        var request = coordinator.TranslateScreenshotAsync(new byte[] { 1 }, "zh-CN", Settings(), CancellationToken.None);

        coordinator.Cancel();

        Assert.True(client.WasScreenshotCanceled(1));
        client.CompleteScreenshot(1, ScreenshotResult("English", "en", "stale result"));
        Assert.Null(await request);
    }

    [Fact]
    public async Task Caller_cancellation_propagates_even_when_the_client_returns_a_result()
    {
        var client = new ControllableTranslationClient();
        var coordinator = new TranslationCoordinator(client);
        using var cancellation = new CancellationTokenSource();

        var request = coordinator.TranslateScreenshotAsync(new byte[] { 1 }, "zh-CN", Settings(), cancellation.Token);

        cancellation.Cancel();
        client.CompleteScreenshot(1, ScreenshotResult("English", "en", "ignored result"));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => request);
    }

    [Fact]
    public async Task Client_cancellation_without_supersession_propagates()
    {
        var client = new ControllableTranslationClient();
        var coordinator = new TranslationCoordinator(client);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var request = coordinator.TranslateScreenshotAsync(new byte[] { 1 }, "zh-CN", Settings(), CancellationToken.None);
        client.CancelScreenshotFromClient(1, cancellation.Token);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => request);
    }

    [Fact]
    public async Task Translation_errors_are_preserved_for_presentation_mapping()
    {
        var client = new ControllableTranslationClient();
        var coordinator = new TranslationCoordinator(client);
        var expected = new TranslationClientException(
            TranslationErrorCode.RateLimited,
            "rate limited");

        var request = coordinator.TranslateScreenshotAsync(new byte[] { 1 }, "zh-CN", Settings(), CancellationToken.None);
        client.FailScreenshot(1, expected);

        var actual = await Assert.ThrowsAsync<TranslationClientException>(() => request);

        Assert.Same(expected, actual);
        Assert.Equal(TranslationErrorCode.RateLimited, actual.Code);
    }

    [Fact]
    public async Task Error_from_a_superseded_request_is_not_observed()
    {
        var client = new ControllableTranslationClient();
        var coordinator = new TranslationCoordinator(client);

        var first = coordinator.TranslateScreenshotAsync(new byte[] { 1 }, "zh-CN", Settings(), CancellationToken.None);
        var second = coordinator.TranslateScreenshotAsync(new byte[] { 2 }, "zh-CN", Settings(), CancellationToken.None);

        client.FailScreenshot(
            1,
            new TranslationClientException(TranslationErrorCode.Network, "stale failure"));
        client.CompleteScreenshot(2, ScreenshotResult("English", "en", "current result"));

        Assert.Null(await first);
        Assert.Equal("current result", (await second)!.Translation);
    }

    [Fact]
    public async Task Reply_request_uses_the_most_recent_detected_language()
    {
        var client = new ControllableTranslationClient
        {
            ReplyResult = new ReplyTranslationResult("fr", "Bonne chance !")
        };
        var coordinator = new TranslationCoordinator(client);
        var screenshot = coordinator.TranslateScreenshotAsync(new byte[] { 1 }, "zh-CN", Settings(), CancellationToken.None);
        client.CompleteScreenshot(1, ScreenshotResult("French", "fr", "translated screenshot"));
        await screenshot;

        var reply = await coordinator.TranslateReplyAsync("good luck!", Settings(), CancellationToken.None);

        Assert.Equal("fr", client.LastReplyTargetLanguageCode);
        Assert.Equal("Bonne chance !", reply!.Translation);
    }

    [Fact]
    public async Task Superseded_screenshot_cannot_replace_the_most_recent_detected_language()
    {
        var client = new ControllableTranslationClient();
        var coordinator = new TranslationCoordinator(client);
        var first = coordinator.TranslateScreenshotAsync(new byte[] { 1 }, "zh-CN", Settings(), CancellationToken.None);
        var second = coordinator.TranslateScreenshotAsync(new byte[] { 2 }, "zh-CN", Settings(), CancellationToken.None);

        client.CompleteScreenshot(2, ScreenshotResult("English", "en", "current result"));
        Assert.Equal("current result", (await second)!.Translation);
        client.CompleteScreenshot(1, ScreenshotResult("French", "fr", "stale result"));
        Assert.Null(await first);

        await coordinator.TranslateReplyAsync("hello", Settings(), CancellationToken.None);

        Assert.Equal("en", client.LastReplyTargetLanguageCode);
    }

    [Fact]
    public async Task Reply_request_can_use_an_explicit_language()
    {
        var client = new ControllableTranslationClient
        {
            ReplyResult = new ReplyTranslationResult("de", "Viel Glueck!")
        };
        var coordinator = new TranslationCoordinator(client);

        var reply = await coordinator.TranslateReplyAsync(
            "good luck!",
            "de",
            Settings(),
            CancellationToken.None);

        Assert.Equal("de", client.LastReplyTargetLanguageCode);
        Assert.Equal("Viel Glueck!", reply!.Translation);
    }

    [Fact]
    public async Task Default_reply_request_requires_a_detected_language()
    {
        var coordinator = new TranslationCoordinator(new ControllableTranslationClient());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            coordinator.TranslateReplyAsync("hello", Settings(), CancellationToken.None));
    }

    private static ModelSettings Settings() =>
        AppSettings.CreateDefault().Model with { ApiKey = "sk-test" };

    private static ScreenshotTranslationResult ScreenshotResult(
        string sourceLanguage,
        string sourceLanguageCode,
        string translation) => new(
            TranslationResultStatus.Ok,
            sourceLanguage,
            sourceLanguageCode,
            translation);

    private sealed class ControllableTranslationClient : ITranslationClient
    {
        private readonly object _gate = new();
        private readonly Dictionary<byte, PendingScreenshot> _screenshots = [];

        public ReplyTranslationResult ReplyResult { get; init; } = new("en", "translated reply");

        public string? LastReplyTargetLanguageCode { get; private set; }

        public Task<ScreenshotTranslationResult> TranslateScreenshotAsync(
            ReadOnlyMemory<byte> pngBytes,
            string targetLanguageCode,
            ModelSettings settings,
            CancellationToken cancellationToken)
        {
            var completion = new TaskCompletionSource<ScreenshotTranslationResult>(
                TaskCreationOptions.RunContinuationsAsynchronously);

            lock (_gate)
            {
                _screenshots.Add(
                    pngBytes.Span[0],
                    new PendingScreenshot(completion, cancellationToken));
            }

            return completion.Task;
        }

        public Task<ReplyTranslationResult> TranslateReplyAsync(
            string input,
            string targetLanguageCode,
            ModelSettings settings,
            CancellationToken cancellationToken)
        {
            LastReplyTargetLanguageCode = targetLanguageCode;
            return Task.FromResult(ReplyResult);
        }

        public Task TestConnectionAsync(ModelSettings settings, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public bool WasScreenshotCanceled(byte key)
        {
            lock (_gate)
            {
                return _screenshots[key].CancellationToken.IsCancellationRequested;
            }
        }

        public void CompleteScreenshot(byte key, ScreenshotTranslationResult result) =>
            GetScreenshot(key).Completion.SetResult(result);

        public void FailScreenshot(byte key, Exception exception) =>
            GetScreenshot(key).Completion.SetException(exception);

        public void CancelScreenshotFromClient(byte key, CancellationToken cancellationToken) =>
            GetScreenshot(key).Completion.SetCanceled(cancellationToken);

        private PendingScreenshot GetScreenshot(byte key)
        {
            lock (_gate)
            {
                return _screenshots[key];
            }
        }

        private sealed record PendingScreenshot(
            TaskCompletionSource<ScreenshotTranslationResult> Completion,
            CancellationToken CancellationToken);
    }
}
