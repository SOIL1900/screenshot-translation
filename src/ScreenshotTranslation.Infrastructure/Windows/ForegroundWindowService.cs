using System.Runtime.InteropServices;

namespace ScreenshotTranslation.Infrastructure.Windows;

public sealed class ForegroundWindowService
{
    private const int SwRestore = 9;

    public nint CaptureForegroundWindow() => GetForegroundWindow();

    public bool RestoreForegroundWindow(nint windowHandle)
    {
        if (windowHandle == nint.Zero)
        {
            return false;
        }

        _ = ShowWindow(windowHandle, SwRestore);
        return SetForegroundWindow(windowHandle);
    }

    [DllImport("user32.dll")]
    private static extern nint GetForegroundWindow();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(nint windowHandle, int command);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(nint windowHandle);
}
