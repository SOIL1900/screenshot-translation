using ScreenshotTranslation.Core.Configuration;

namespace ScreenshotTranslation.Core.Translation;

public sealed class TranslationCoordinator(ITranslationClient client)
{
    private readonly object _gate = new();
    private readonly ITranslationClient _client = client ?? throw new ArgumentNullException(nameof(client));
    private long _requestVersion;
    private CancellationTokenSource? _activeRequestSource;
    private string? _mostRecentSourceLanguageCode;

    public Task<ScreenshotTranslationResult?> TranslateScreenshotAsync(
        ReadOnlyMemory<byte> pngBytes,
        string targetLanguageCode,
        ModelSettings settings,
        CancellationToken cancellationToken) =>
        ExecuteLatestAsync(
            token => _client.TranslateScreenshotAsync(pngBytes, targetLanguageCode, settings, token),
            cancellationToken,
            result =>
            {
                if (result.Status == TranslationResultStatus.Ok &&
                    !string.IsNullOrWhiteSpace(result.SourceLanguageCode))
                {
                    _mostRecentSourceLanguageCode = result.SourceLanguageCode;
                }
            });

    public Task<ReplyTranslationResult?> TranslateReplyAsync(
        string input,
        ModelSettings settings,
        CancellationToken cancellationToken)
    {
        string targetLanguageCode;

        lock (_gate)
        {
            targetLanguageCode = _mostRecentSourceLanguageCode ??
                throw new InvalidOperationException(
                    "A successful screenshot translation is required before translating a reply.");
        }

        return TranslateReplyAsync(input, targetLanguageCode, settings, cancellationToken);
    }

    public Task<ReplyTranslationResult?> TranslateReplyAsync(
        string input,
        string targetLanguageCode,
        ModelSettings settings,
        CancellationToken cancellationToken) =>
        ExecuteLatestAsync(
            token => _client.TranslateReplyAsync(input, targetLanguageCode, settings, token),
            cancellationToken);

    public void Cancel()
    {
        CancellationTokenSource? source;

        lock (_gate)
        {
            _requestVersion++;
            source = _activeRequestSource;
            _activeRequestSource = null;
        }

        CancelAndDispose(source);
    }

    private async Task<TResult?> ExecuteLatestAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation,
        CancellationToken callerCancellationToken,
        Action<TResult>? acceptResult = null)
        where TResult : class
    {
        Request request = StartRequest(callerCancellationToken);

        try
        {
            TResult result = await operation(request.Token).ConfigureAwait(false);
            callerCancellationToken.ThrowIfCancellationRequested();

            lock (_gate)
            {
                if (request.Version != _requestVersion)
                {
                    return null;
                }

                acceptResult?.Invoke(result);
            }

            return result;
        }
        catch (Exception) when (IsSuperseded(request.Version))
        {
            callerCancellationToken.ThrowIfCancellationRequested();
            return null;
        }
        finally
        {
            CompleteRequest(request);
        }
    }

    private Request StartRequest(CancellationToken callerCancellationToken)
    {
        var source = CancellationTokenSource.CreateLinkedTokenSource(callerCancellationToken);
        CancellationToken token = source.Token;
        CancellationTokenSource? previousSource;
        long version;

        lock (_gate)
        {
            version = ++_requestVersion;
            previousSource = _activeRequestSource;
            _activeRequestSource = source;
        }

        CancelAndDispose(previousSource);
        return new Request(version, source, token);
    }

    private bool IsSuperseded(long version)
    {
        lock (_gate)
        {
            return version != _requestVersion;
        }
    }

    private void CompleteRequest(Request request)
    {
        CancellationTokenSource? source = null;

        lock (_gate)
        {
            if (request.Version == _requestVersion &&
                ReferenceEquals(_activeRequestSource, request.Source))
            {
                _activeRequestSource = null;
                source = request.Source;
            }
        }

        source?.Dispose();
    }

    private static void CancelAndDispose(CancellationTokenSource? source)
    {
        if (source is null)
        {
            return;
        }

        try
        {
            source.Cancel();
        }
        finally
        {
            source.Dispose();
        }
    }

    private sealed class Request(
        long version,
        CancellationTokenSource source,
        CancellationToken token)
    {
        public long Version { get; } = version;

        public CancellationTokenSource Source { get; } = source;

        public CancellationToken Token { get; } = token;
    }
}
