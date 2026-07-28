using System.Net;
using System.Net.Http.Headers;
using System.Text;
using ScreenshotTranslation.Core.Configuration;
using ScreenshotTranslation.Core.Translation;
using ScreenshotTranslation.Infrastructure.Translation;

namespace ScreenshotTranslation.Infrastructure.Tests.Translation;

public sealed class OpenAiTranslationClientTests
{
    [Fact]
    public async Task Screenshot_translation_posts_to_configured_endpoint_with_per_request_bearer_token()
    {
        CapturedRequest? captured = null;
        using var httpClient = new HttpClient(new StubHttpMessageHandler(async (request, cancellationToken) =>
        {
            captured = await CapturedRequest.CreateAsync(request, cancellationToken);
            return JsonResponse("""
                {
                  "choices": [{
                    "message": {
                      "reasoning_content": "private chain of thought",
                      "content": "{\"status\":\"ok\",\"sourceLanguage\":\"English\",\"sourceLanguageCode\":\"en\",\"translation\":\"你好\"}"
                    }
                  }]
                }
                """);
        }));
        var settings = Settings() with { BaseUrl = "https://example.test/openai/v1/" };
        var client = new OpenAiTranslationClient(httpClient);

        var result = await client.TranslateScreenshotAsync(
            TestPngFactory.CreateSolid(32, 16),
            "zh-CN",
            settings,
            CancellationToken.None);

        Assert.NotNull(captured);
        Assert.Equal(new Uri("https://example.test/openai/v1/chat/completions"), captured.Uri);
        Assert.Equal("Bearer", captured.Authorization?.Scheme);
        Assert.Equal("sk-test", captured.Authorization?.Parameter);
        Assert.Contains("data:image/png;base64,", captured.Body);
        Assert.Equal("你好", result.Translation);
        Assert.DoesNotContain("private chain of thought", result.Translation);
        Assert.Null(httpClient.DefaultRequestHeaders.Authorization);
    }

    [Fact]
    public async Task Reply_translation_parses_the_assistant_content()
    {
        using var httpClient = ClientReturning("""
            {"choices":[{"message":{"content":"  Viel Glück!  "}}]}
            """);
        var client = new OpenAiTranslationClient(httpClient);

        var result = await client.TranslateReplyAsync("good luck", "de", Settings(), CancellationToken.None);

        Assert.Equal("de", result.TargetLanguageCode);
        Assert.Equal("Viel Glück!", result.Translation);
    }

    [Fact]
    public async Task Connection_test_succeeds_for_non_empty_assistant_content()
    {
        using var httpClient = ClientReturning("{\"choices\":[{\"message\":{\"content\":\"OK\"}}]}");
        var client = new OpenAiTranslationClient(httpClient);

        await client.TestConnectionAsync(Settings(), CancellationToken.None);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, TranslationErrorCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden, TranslationErrorCode.Unauthorized)]
    [InlineData(HttpStatusCode.TooManyRequests, TranslationErrorCode.RateLimited)]
    [InlineData(HttpStatusCode.InternalServerError, TranslationErrorCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.BadGateway, TranslationErrorCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.ServiceUnavailable, TranslationErrorCode.ServiceUnavailable)]
    public async Task Http_failures_map_to_stable_error_codes(HttpStatusCode status, TranslationErrorCode expected)
    {
        using var httpClient = new HttpClient(new StubHttpMessageHandler(
            (_, _) => Task.FromResult(new HttpResponseMessage(status))));
        var client = new OpenAiTranslationClient(httpClient);

        var exception = await Assert.ThrowsAsync<TranslationClientException>(
            () => client.TestConnectionAsync(Settings(), CancellationToken.None));

        Assert.Equal(expected, exception.Code);
    }

    [Fact]
    public async Task Configured_timeout_maps_to_timeout_error()
    {
        using var httpClient = new HttpClient(new StubHttpMessageHandler(async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException("Unreachable");
        }));
        var client = new OpenAiTranslationClient(httpClient);

        var exception = await Assert.ThrowsAsync<TranslationClientException>(
            () => client.TestConnectionAsync(Settings() with { RequestTimeoutSeconds = 0 }, CancellationToken.None));

        Assert.Equal(TranslationErrorCode.Timeout, exception.Code);
    }

    [Fact]
    public async Task Network_failure_maps_to_network_error()
    {
        var failure = new HttpRequestException("DNS failed");
        using var httpClient = new HttpClient(new StubHttpMessageHandler((_, _) => Task.FromException<HttpResponseMessage>(failure)));
        var client = new OpenAiTranslationClient(httpClient);

        var exception = await Assert.ThrowsAsync<TranslationClientException>(
            () => client.TestConnectionAsync(Settings(), CancellationToken.None));

        Assert.Equal(TranslationErrorCode.Network, exception.Code);
        Assert.Same(failure, exception.InnerException);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("{}")]
    [InlineData("{\"choices\":[]}")]
    [InlineData("{\"choices\":[{\"message\":{\"reasoning_content\":\"secret\"}}]}")]
    [InlineData("{\"choices\":[{\"message\":{\"content\":\"\"}}]}")]
    public async Task Invalid_response_envelopes_map_to_invalid_response(string responseJson)
    {
        using var httpClient = ClientReturning(responseJson);
        var client = new OpenAiTranslationClient(httpClient);

        var exception = await Assert.ThrowsAsync<TranslationClientException>(
            () => client.TestConnectionAsync(Settings(), CancellationToken.None));

        Assert.Equal(TranslationErrorCode.InvalidResponse, exception.Code);
        Assert.DoesNotContain("secret", exception.Message);
    }

    [Fact]
    public async Task Qualified_refusal_content_is_invalid_and_reasoning_is_not_surfaced()
    {
        using var httpClient = ClientReturning("""
            {
              "choices": [{
                "message": {
                  "reasoning_content": "secret reasoning",
                  "content": "Unfortunately, I cannot translate this screenshot."
                }
              }]
            }
            """);
        var client = new OpenAiTranslationClient(httpClient);

        var exception = await Assert.ThrowsAsync<TranslationClientException>(() =>
            client.TranslateScreenshotAsync(
                TestPngFactory.CreateSolid(32, 16),
                "zh-CN",
                Settings(),
                CancellationToken.None));

        Assert.Equal(TranslationErrorCode.InvalidResponse, exception.Code);
        Assert.DoesNotContain("secret reasoning", exception.Message);
    }

    [Fact]
    public async Task Caller_cancellation_is_not_remapped_as_timeout()
    {
        using var source = new CancellationTokenSource();
        source.Cancel();
        using var httpClient = new HttpClient(new StubHttpMessageHandler((_, cancellationToken) =>
            Task.FromCanceled<HttpResponseMessage>(cancellationToken)));
        var client = new OpenAiTranslationClient(httpClient);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => client.TestConnectionAsync(Settings(), source.Token));
    }

    [Fact]
    public async Task Screenshot_translation_yields_while_request_image_normalization_is_held()
    {
        var normalizer = new BlockingRequestImageNormalizer();
        var sendCount = 0;
        using var httpClient = new HttpClient(new StubHttpMessageHandler((_, _) =>
        {
            sendCount++;
            return Task.FromResult(JsonResponse(ScreenshotResponseJson));
        }));
        var client = new OpenAiTranslationClient(httpClient, normalizer);

        var translation = client.TranslateScreenshotAsync(new byte[] { 1 }, "zh-CN", Settings(), CancellationToken.None);
        await normalizer.Started.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.False(translation.IsCompleted);
        Assert.Equal(0, sendCount);

        normalizer.Release();
        var result = await translation;

        Assert.Equal("translated", result.Translation);
        Assert.Equal(1, sendCount);
    }

    [Fact]
    public async Task Caller_cancellation_stops_held_normalization_before_network()
    {
        var normalizer = new BlockingRequestImageNormalizer();
        var sendCount = 0;
        using var httpClient = new HttpClient(new StubHttpMessageHandler((_, _) =>
        {
            sendCount++;
            return Task.FromResult(JsonResponse(ScreenshotResponseJson));
        }));
        var client = new OpenAiTranslationClient(httpClient, normalizer);
        using var cancellation = new CancellationTokenSource();

        var translation = client.TranslateScreenshotAsync(new byte[] { 1 }, "zh-CN", Settings(), cancellation.Token);
        await normalizer.Started.WaitAsync(TimeSpan.FromSeconds(2));
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => translation);
        Assert.True(normalizer.ReceivedToken.IsCancellationRequested);
        Assert.Equal(0, sendCount);
    }

    [Fact]
    public async Task Coordinator_cancel_stops_held_normalization_before_network()
    {
        var normalizer = new BlockingRequestImageNormalizer();
        var sendCount = 0;
        using var httpClient = new HttpClient(new StubHttpMessageHandler((_, _) =>
        {
            sendCount++;
            return Task.FromResult(JsonResponse(ScreenshotResponseJson));
        }));
        var coordinator = new TranslationCoordinator(new OpenAiTranslationClient(httpClient, normalizer));

        var translation = coordinator.TranslateScreenshotAsync(new byte[] { 1 }, "zh-CN", Settings(), CancellationToken.None);
        await normalizer.Started.WaitAsync(TimeSpan.FromSeconds(2));
        coordinator.Cancel();

        Assert.Null(await translation);
        Assert.True(normalizer.ReceivedToken.IsCancellationRequested);
        Assert.Equal(0, sendCount);
    }

    private static ModelSettings Settings() => AppSettings.CreateDefault().Model with { ApiKey = "sk-test" };

    private const string ScreenshotResponseJson =
        "{\"choices\":[{\"message\":{\"content\":\"{\\\"status\\\":\\\"ok\\\",\\\"sourceLanguage\\\":\\\"English\\\",\\\"sourceLanguageCode\\\":\\\"en\\\",\\\"translation\\\":\\\"translated\\\"}\"}}]}";

    private static HttpClient ClientReturning(string json) => new(
        new StubHttpMessageHandler((_, _) => Task.FromResult(JsonResponse(json))));

    private static HttpResponseMessage JsonResponse(string json) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json")
    };

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> sendAsync) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => sendAsync(request, cancellationToken);
    }

    private sealed class BlockingRequestImageNormalizer : IRequestImageNormalizer
    {
        private readonly TaskCompletionSource _started =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<string> _completion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task Started => _started.Task;

        public CancellationToken ReceivedToken { get; private set; }

        public async Task<string> NormalizeToDataUrlAsync(
            ReadOnlyMemory<byte> pngBytes,
            CancellationToken cancellationToken)
        {
            ReceivedToken = cancellationToken;
            _started.TrySetResult();
            return await _completion.Task.WaitAsync(cancellationToken);
        }

        public void Release() => _completion.TrySetResult("data:image/png;base64,iVBORw0KGgo=");
    }

    private sealed record CapturedRequest(Uri? Uri, AuthenticationHeaderValue? Authorization, string Body)
    {
        public static async Task<CapturedRequest> CreateAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => new(
                request.RequestUri,
                request.Headers.Authorization,
                request.Content is null
                    ? string.Empty
                    : await request.Content.ReadAsStringAsync(cancellationToken));
    }
}
