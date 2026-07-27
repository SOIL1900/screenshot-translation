using System.IO.Pipes;
using System.Text;
using System.Windows.Threading;

namespace ScreenshotTranslation.Infrastructure.Windows;

public sealed class SingleInstanceCoordinator : IDisposable, IAsyncDisposable
{
    public const string MutexName = "ScreenshotTranslation.SingleInstance";
    public const string ActivationPipeName = "ScreenshotTranslation.Activation";
    public const string ShowSettingsCommand = "SHOW_SETTINGS";

    private const int ConnectionTimeoutMilliseconds = 2_000;

    private readonly Dispatcher _dispatcher;
    private readonly Mutex _mutex;
    private readonly bool _ownsMutex;
    private CancellationTokenSource? _listenerCancellation;
    private Task? _listenerTask;
    private int _disposed;

    public SingleInstanceCoordinator(Dispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        if (!dispatcher.CheckAccess())
        {
            throw new InvalidOperationException(
                "The single-instance coordinator must be created on its WPF dispatcher thread.");
        }

        _dispatcher = dispatcher;
        _mutex = new Mutex(initiallyOwned: true, MutexName, out var createdNew);
        _ownsMutex = createdNew;
        IsPrimaryInstance = createdNew;
    }

    public bool IsPrimaryInstance { get; }

    public void StartListening(Action activationCallback)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        ArgumentNullException.ThrowIfNull(activationCallback);
        if (!IsPrimaryInstance)
        {
            throw new InvalidOperationException("Only the primary instance can listen for activation.");
        }

        if (_listenerTask is not null)
        {
            throw new InvalidOperationException("The activation listener has already been started.");
        }

        _listenerCancellation = new CancellationTokenSource();
        _listenerTask = ListenAsync(activationCallback, _listenerCancellation.Token);
    }

    public async Task<bool> NotifyPrimaryAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        if (IsPrimaryInstance)
        {
            throw new InvalidOperationException("The primary instance cannot notify itself.");
        }

        try
        {
            using var client = new NamedPipeClientStream(
                ".",
                ActivationPipeName,
                PipeDirection.Out,
                PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
            await client.ConnectAsync(ConnectionTimeoutMilliseconds, cancellationToken).ConfigureAwait(false);

            await using var writer = new StreamWriter(
                client,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                bufferSize: 1024,
                leaveOpen: true)
            {
                AutoFlush = true
            };
            await writer.WriteLineAsync(ShowSettingsCommand.AsMemory(), cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        try
        {
            StopListening();
        }
        finally
        {
            ReleaseMutexAndDispose();
            GC.SuppressFinalize(this);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        try
        {
            await StopListeningAsync().ConfigureAwait(false);
        }
        finally
        {
            await ReleaseMutexAndDisposeAsync().ConfigureAwait(false);
            GC.SuppressFinalize(this);
        }
    }

    private void StopListening()
    {
        if (_listenerCancellation is not null)
        {
            try
            {
                _listenerCancellation.Cancel();
                if (_listenerTask is not null)
                {
                    try
                    {
                        _listenerTask.GetAwaiter().GetResult();
                    }
                    catch (OperationCanceledException)
                    {
                    }
                }
            }
            finally
            {
                _listenerCancellation.Dispose();
            }
        }
    }

    private async Task StopListeningAsync()
    {
        if (_listenerCancellation is null)
        {
            return;
        }

        try
        {
            await _listenerCancellation.CancelAsync().ConfigureAwait(false);
            if (_listenerTask is not null)
            {
                try
                {
                    await _listenerTask.ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                }
            }
        }
        finally
        {
            _listenerCancellation.Dispose();
        }
    }

    private async Task ListenAsync(Action activationCallback, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await ListenForOneActivationAsync(activationCallback, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (IOException) when (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(100, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private async Task ListenForOneActivationAsync(
        Action activationCallback,
        CancellationToken cancellationToken)
    {
        await using var server = new NamedPipeServerStream(
            ActivationPipeName,
            PipeDirection.In,
            maxNumberOfServerInstances: 1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        await server.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);

        using var reader = new StreamReader(
            server,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            detectEncodingFromByteOrderMarks: false,
            bufferSize: 1024,
            leaveOpen: true);
        var command = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
        if (string.Equals(command, ShowSettingsCommand, StringComparison.Ordinal))
        {
            _ = _dispatcher.BeginInvoke(DispatcherPriority.Normal, activationCallback);
        }
    }

    private void ReleaseMutexAndDispose()
    {
        if (_dispatcher.CheckAccess())
        {
            ReleaseMutexAndDisposeCore();
        }
        else
        {
            _dispatcher.Invoke(ReleaseMutexAndDisposeCore);
        }
    }

    private async Task ReleaseMutexAndDisposeAsync()
    {
        if (_dispatcher.CheckAccess())
        {
            ReleaseMutexAndDisposeCore();
            return;
        }

        await _dispatcher.InvokeAsync(ReleaseMutexAndDisposeCore).Task.ConfigureAwait(false);
    }

    private void ReleaseMutexAndDisposeCore()
    {
        if (_ownsMutex)
        {
            _mutex.ReleaseMutex();
        }

        _mutex.Dispose();
    }
}
