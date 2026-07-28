using System.Windows.Threading;
using ScreenshotTranslation.Infrastructure.Windows;

namespace ScreenshotTranslation.Infrastructure.Tests.Windows;

public sealed class SingleInstanceCoordinatorTests
{
    private static int _coordinationNameSequence;

    [Fact]
    public Task Starting_listener_twice_is_rejected()
    {
        return DispatcherTestHost.RunAsync(dispatcher =>
        {
            var names = CreateUniqueCoordinationNames();
            using var coordinator = new SingleInstanceCoordinator(dispatcher, names.Mutex, names.Pipe);
            Assert.True(coordinator.IsPrimaryInstance);

            coordinator.StartListening(() => { });

            Assert.Throws<InvalidOperationException>(() => coordinator.StartListening(() => { }));
            return Task.CompletedTask;
        });
    }

    [Fact]
    public Task Dispose_prevents_listener_from_starting_later()
    {
        return DispatcherTestHost.RunAsync(dispatcher =>
        {
            var names = CreateUniqueCoordinationNames();
            var coordinator = new SingleInstanceCoordinator(dispatcher, names.Mutex, names.Pipe);

            coordinator.Dispose();

            Assert.Throws<ObjectDisposedException>(() => coordinator.StartListening(() => { }));
            return Task.CompletedTask;
        });
    }

    [Fact]
    public Task DisposeAsync_stops_started_listener_and_releases_coordination_names()
    {
        return DispatcherTestHost.RunAsync(async dispatcher =>
        {
            var names = CreateUniqueCoordinationNames();
            var coordinator = new SingleInstanceCoordinator(dispatcher, names.Mutex, names.Pipe);
            Assert.True(coordinator.IsPrimaryInstance);
            coordinator.StartListening(() => { });

            await coordinator.DisposeAsync();

            await using var replacement = new SingleInstanceCoordinator(dispatcher, names.Mutex, names.Pipe);
            Assert.True(replacement.IsPrimaryInstance);
            replacement.StartListening(() => { });
        });
    }

    private static (string Mutex, string Pipe) CreateUniqueCoordinationNames()
    {
        var sequence = Interlocked.Increment(ref _coordinationNameSequence);
        var prefix = $"ScreenshotTranslation.Tests.{Environment.ProcessId}.{sequence}";
        return ($"{prefix}.Mutex", $"{prefix}.Pipe");
    }
}

internal static class DispatcherTestHost
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(10);

    public static async Task RunAsync(Func<Dispatcher, Task> test)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() => RunDispatcher(test, completion))
        {
            IsBackground = true,
            Name = "ScreenshotTranslation.Tests.Dispatcher"
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        await completion.Task.WaitAsync(TestTimeout);
        Assert.True(thread.Join(TestTimeout), "The test dispatcher thread did not stop.");
    }

    private static void RunDispatcher(
        Func<Dispatcher, Task> test,
        TaskCompletionSource completion)
    {
        Exception? failure = null;
        try
        {
            var dispatcher = Dispatcher.CurrentDispatcher;
            _ = dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(async () =>
            {
                try
                {
                    await test(dispatcher);
                }
                catch (Exception exception)
                {
                    failure = exception;
                }
                finally
                {
                    dispatcher.BeginInvokeShutdown(DispatcherPriority.Send);
                }
            }));
            Dispatcher.Run();
        }
        catch (Exception exception)
        {
            failure = exception;
        }

        if (failure is null)
        {
            completion.TrySetResult();
        }
        else
        {
            completion.TrySetException(failure);
        }
    }
}
