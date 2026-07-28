using System.Runtime.InteropServices;

namespace ScreenshotTranslation.Infrastructure.Windows;

internal interface IForegroundWindowNativeMethods
{
    nint GetForegroundWindow();

    bool SetForegroundWindow(nint windowHandle);
}

public sealed class ForegroundWindowService
{
    private readonly IForegroundWindowNativeMethods _nativeMethods;

    public ForegroundWindowService()
        : this(Win32ForegroundWindowNativeMethods.Instance)
    {
    }

    internal ForegroundWindowService(IForegroundWindowNativeMethods nativeMethods)
    {
        _nativeMethods = nativeMethods ?? throw new ArgumentNullException(nameof(nativeMethods));
    }

    public nint CaptureForegroundWindow() => _nativeMethods.GetForegroundWindow();

    public bool RestoreForegroundWindow(nint windowHandle)
    {
        if (windowHandle == nint.Zero)
        {
            return false;
        }

        return _nativeMethods.SetForegroundWindow(windowHandle);
    }

    private sealed class Win32ForegroundWindowNativeMethods : IForegroundWindowNativeMethods
    {
        public static Win32ForegroundWindowNativeMethods Instance { get; } = new();

        public nint GetForegroundWindow() => NativeGetForegroundWindow();

        public bool SetForegroundWindow(nint windowHandle) => NativeSetForegroundWindow(windowHandle);

        [DllImport("user32.dll", EntryPoint = "GetForegroundWindow")]
        private static extern nint NativeGetForegroundWindow();

        [DllImport("user32.dll", EntryPoint = "SetForegroundWindow")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool NativeSetForegroundWindow(nint windowHandle);
    }
}
