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

    private readonly object _lifecycleLock = new();
    private readonly Dispatcher _dispatcher;
    private readonly string _activationPipeName;
    private readonly bool _ownsMutex;
    private Mutex? _mutex;
    private CancellationTokenSource? _listenerCancellation;
    private Task? _listenerTask;
    private LifecycleState _lifecycleState;

    public SingleInstanceCoordinator(Dispatcher dispatcher)
        : this(dispatcher, MutexName, ActivationPipeName)
    {
    }

    internal SingleInstanceCoordinator(
        Dispatcher dispatcher,
        string mutexName,
        string activationPipeName)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentException.ThrowIfNullOrWhiteSpace(mutexName);
        ArgumentException.ThrowIfNullOrWhiteSpace(activationPipeName);
        if (!dispatcher.CheckAccess())
        {
            throw new InvalidOperationException(
                "The single-instance coordinator must be created on its WPF dispatcher thread.");
        }

        _dispatcher = dispatcher;
        _activationPipeName = activationPipeName;
        _mutex = new Mutex(initiallyOwned: true, mutexName, out var createdNew);
        _ownsMutex = createdNew;
        IsPrimaryInstance = createdNew;
    }

    public bool IsPrimaryInstance { get; }

    public void StartListening(Action activationCallback)
    {
        ArgumentNullException.ThrowIfNull(activationCallback);

        lock (_lifecycleLock)
        {
            ObjectDisposedException.ThrowIf(_lifecycleState == LifecycleState.Disposed, this);
            if (!IsPrimaryInstance)
            {
                throw new InvalidOperationException("Only the primary instance can listen for activation.");
            }

            if (_lifecycleState == LifecycleState.Listening)
            {
                throw new InvalidOperationException("The activation listener has already been started.");
            }

            var listenerCancellation = new CancellationTokenSource();
            _listenerCancellation = listenerCancellation;
            _listenerTask = ListenAsync(activationCallback, listenerCancellation.Token);
            _lifecycleState = LifecycleState.Listening;
        }
    }

    public async Task<bool> NotifyPrimaryAsync(CancellationToken cancellationToken = default)
    {
        lock (_lifecycleLock)
        {
            ObjectDisposedException.ThrowIf(_lifecycleState == LifecycleState.Disposed, this);
            if (IsPrimaryInstance)
            {
                throw new InvalidOperationException("The primary instance cannot notify itself.");
            }
        }

        try
        {
            using var client = new NamedPipeClientStream(
                ".",
                _activationPipeName,
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
        var resources = BeginDispose();
        if (resources is null)
        {
            return;
        }

        try
        {
            StopListening(resources.ListenerCancellation, resources.ListenerTask);
        }
        finally
        {
            ReleaseMutexAndDispose(resources.Mutex);
            GC.SuppressFinalize(this);
        }
    }

    public async ValueTask DisposeAsync()
    {
        var resources = BeginDispose();
        if (resources is null)
        {
            return;
        }

        try
        {
            await StopListeningAsync(
                    resources.ListenerCancellation,
                    resources.ListenerTask)
                .ConfigureAwait(false);
        }
        finally
        {
            await ReleaseMutexAndDisposeAsync(resources.Mutex).ConfigureAwait(false);
            GC.SuppressFinalize(this);
        }
    }

    private DisposalResources? BeginDispose()
    {
        lock (_lifecycleLock)
        {
            if (_lifecycleState == LifecycleState.Disposed)
            {
                return null;
            }

            _lifecycleState = LifecycleState.Disposed;
            var mutex = _mutex ?? throw new InvalidOperationException("The instance mutex is unavailable.");
            var resources = new DisposalResources(
                _listenerCancellation,
                _listenerTask,
                mutex);
            _listenerCancellation = null;
            _listenerTask = null;
            _mutex = null;
            return resources;
        }
    }

    private static void StopListening(
        CancellationTokenSource? listenerCancellation,
        Task? listenerTask)
    {
        if (listenerCancellation is null)
        {
            return;
        }

        try
        {
            listenerCancellation.Cancel();
            if (listenerTask is not null)
            {
                try
                {
                    listenerTask.GetAwaiter().GetResult();
                }
                catch (OperationCanceledException)
                {
                }
            }
        }
        finally
        {
            listenerCancellation.Dispose();
        }
    }

    private static async Task StopListeningAsync(
        CancellationTokenSource? listenerCancellation,
        Task? listenerTask)
    {
        if (listenerCancellation is null)
        {
            return;
        }

        try
        {
            await listenerCancellation.CancelAsync().ConfigureAwait(false);
            if (listenerTask is not null)
            {
                try
                {
                    await listenerTask.ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                }
            }
        }
        finally
        {
            listenerCancellation.Dispose();
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
            _activationPipeName,
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

    private void ReleaseMutexAndDispose(Mutex mutex)
    {
        if (_dispatcher.CheckAccess())
        {
            ReleaseMutexAndDisposeCore(mutex);
        }
        else
        {
            _dispatcher.Invoke(() => ReleaseMutexAndDisposeCore(mutex));
        }
    }

    private async Task ReleaseMutexAndDisposeAsync(Mutex mutex)
    {
        if (_dispatcher.CheckAccess())
        {
            ReleaseMutexAndDisposeCore(mutex);
            return;
        }

        await _dispatcher.InvokeAsync(() => ReleaseMutexAndDisposeCore(mutex)).Task.ConfigureAwait(false);
    }

    private void ReleaseMutexAndDisposeCore(Mutex mutex)
    {
        try
        {
            if (_ownsMutex)
            {
                mutex.ReleaseMutex();
            }
        }
        finally
        {
            mutex.Dispose();
        }
    }

    private enum LifecycleState
    {
        Ready,
        Listening,
        Disposed
    }

    private sealed record DisposalResources(
        CancellationTokenSource? ListenerCancellation,
        Task? ListenerTask,
        Mutex Mutex);
}
