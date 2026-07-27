using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using ScreenshotTranslation.Core.Configuration;
using ScreenshotTranslation.Core.Translation;

namespace ScreenshotTranslation.Infrastructure.Translation;

public sealed class OpenAiTranslationClient(HttpClient httpClient) : ITranslationClient
{
    private readonly HttpClient _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));

    public async Task<ScreenshotTranslationResult> TranslateScreenshotAsync(
        ReadOnlyMemory<byte> pngBytes,
        string targetLanguageCode,
        ModelSettings settings,
        CancellationToken cancellationToken)
    {
        JsonObject request = OpenAiRequestFactory.CreateScreenshotRequest(
            settings,
            pngBytes,
            targetLanguageCode);
        string content = await SendAsync(request, settings, cancellationToken).ConfigureAwait(false);
        return OpenAiResponseParser.ParseScreenshotContent(content);
    }

    public async Task<ReplyTranslationResult> TranslateReplyAsync(
        string input,
        string targetLanguageCode,
        ModelSettings settings,
        CancellationToken cancellationToken)
    {
        JsonObject request = OpenAiRequestFactory.CreateReplyRequest(settings, input, targetLanguageCode);
        string content = await SendAsync(request, settings, cancellationToken).ConfigureAwait(false);
        return OpenAiResponseParser.ParseReplyContent(content, targetLanguageCode);
    }

    public async Task TestConnectionAsync(ModelSettings settings, CancellationToken cancellationToken)
    {
        JsonObject request = OpenAiRequestFactory.CreateConnectionTestRequest(settings);
        _ = await SendAsync(request, settings, cancellationToken).ConfigureAwait(false);
    }

    private async Task<string> SendAsync(
        JsonObject payload,
        ModelSettings settings,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(TimeSpan.FromSeconds(settings.RequestTimeoutSeconds));

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, CreateEndpoint(settings.BaseUrl))
            {
                Headers =
                {
                    Authorization = new AuthenticationHeaderValue("Bearer", settings.ApiKey)
                },
                Content = new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json")
            };

            using HttpResponseMessage response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                timeoutSource.Token).ConfigureAwait(false);

            ThrowForFailureStatus(response.StatusCode);

            string responseJson = await response.Content
                .ReadAsStringAsync(timeoutSource.Token)
                .ConfigureAwait(false);
            return OpenAiResponseParser.ExtractAssistantContent(responseJson);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TranslationClientException(
                TranslationErrorCode.Timeout,
                "The translation request timed out.");
        }
        catch (HttpRequestException exception)
        {
            throw new TranslationClientException(
                TranslationErrorCode.Network,
                "The translation service could not be reached.",
                exception);
        }
    }

    private static Uri CreateEndpoint(string baseUrl)
    {
        string normalizedBaseUrl = baseUrl.TrimEnd('/');
        return new Uri($"{normalizedBaseUrl}/chat/completions", UriKind.Absolute);
    }

    private static void ThrowForFailureStatus(HttpStatusCode statusCode)
    {
        if ((int)statusCode is >= 200 and < 300)
        {
            return;
        }

        TranslationErrorCode code = statusCode switch
        {
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => TranslationErrorCode.Unauthorized,
            HttpStatusCode.TooManyRequests => TranslationErrorCode.RateLimited,
            HttpStatusCode.RequestTimeout => TranslationErrorCode.Timeout,
            >= HttpStatusCode.InternalServerError => TranslationErrorCode.ServiceUnavailable,
            _ => TranslationErrorCode.ServiceUnavailable
        };

        throw new TranslationClientException(code, $"The translation service returned HTTP {(int)statusCode}.");
    }
}
