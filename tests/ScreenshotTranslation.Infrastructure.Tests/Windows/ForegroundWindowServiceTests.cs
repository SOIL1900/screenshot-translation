using ScreenshotTranslation.Infrastructure.Windows;

namespace ScreenshotTranslation.Infrastructure.Tests.Windows;

public sealed class ForegroundWindowServiceTests
{
    [Fact]
    public void Capture_returns_the_current_foreground_window()
    {
        var nativeMethods = new FakeForegroundWindowNativeMethods
        {
            ForegroundWindow = new nint(123)
        };
        var service = new ForegroundWindowService(nativeMethods);

        var capturedWindow = service.CaptureForegroundWindow();

        Assert.Equal(new nint(123), capturedWindow);
    }

    [Fact]
    public void Restore_only_reactivates_the_window_without_changing_its_display_state()
    {
        var nativeMethods = new FakeForegroundWindowNativeMethods
        {
            SetForegroundWindowResult = true
        };
        var service = new ForegroundWindowService(nativeMethods);

        var restored = service.RestoreForegroundWindow(new nint(456));

        Assert.True(restored);
        Assert.Equal([new nint(456)], nativeMethods.ActivatedWindows);
    }

    [Fact]
    public void Restore_rejects_an_empty_window_handle()
    {
        var nativeMethods = new FakeForegroundWindowNativeMethods();
        var service = new ForegroundWindowService(nativeMethods);

        var restored = service.RestoreForegroundWindow(nint.Zero);

        Assert.False(restored);
        Assert.Empty(nativeMethods.ActivatedWindows);
    }

    private sealed class FakeForegroundWindowNativeMethods : IForegroundWindowNativeMethods
    {
        public nint ForegroundWindow { get; init; }

        public bool SetForegroundWindowResult { get; init; }

        public List<nint> ActivatedWindows { get; } = [];

        public nint GetForegroundWindow() => ForegroundWindow;

        public bool SetForegroundWindow(nint windowHandle)
        {
            ActivatedWindows.Add(windowHandle);
            return SetForegroundWindowResult;
        }
    }
}
