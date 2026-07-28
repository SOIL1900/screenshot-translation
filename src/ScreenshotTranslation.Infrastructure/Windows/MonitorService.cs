using System.ComponentModel;
using System.Runtime.InteropServices;
using ScreenshotTranslation.Core.Geometry;

namespace ScreenshotTranslation.Infrastructure.Windows;

public sealed class MonitorService : IMonitorService
{
    private const uint MonitorDefaultToNearest = 0x00000002;

    public MonitorBounds GetMonitorUnderCursor()
    {
        if (!GetCursorPos(out var cursorPosition))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Unable to get the cursor position.");
        }

        var monitorHandle = MonitorFromPoint(cursorPosition, MonitorDefaultToNearest);
        if (monitorHandle == nint.Zero)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Unable to locate the monitor under the cursor.");
        }

        var monitorInfo = new MonitorInfo
        {
            Size = (uint)Marshal.SizeOf<MonitorInfo>()
        };
        if (!GetMonitorInfo(monitorHandle, ref monitorInfo))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Unable to read the monitor bounds.");
        }

        var width = checked(monitorInfo.Monitor.Right - monitorInfo.Monitor.Left);
        var height = checked(monitorInfo.Monitor.Bottom - monitorInfo.Monitor.Top);
        var workAreaWidth = checked(monitorInfo.WorkArea.Right - monitorInfo.WorkArea.Left);
        var workAreaHeight = checked(monitorInfo.WorkArea.Bottom - monitorInfo.WorkArea.Top);
        return new MonitorBounds(
            monitorHandle,
            new PixelRect(monitorInfo.Monitor.Left, monitorInfo.Monitor.Top, width, height),
            new PixelRect(
                monitorInfo.WorkArea.Left,
                monitorInfo.WorkArea.Top,
                workAreaWidth,
                workAreaHeight));
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out NativePoint point);

    [DllImport("user32.dll")]
    private static extern nint MonitorFromPoint(NativePoint point, uint flags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(nint monitor, ref MonitorInfo monitorInfo);

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct NativePoint
    {
        public readonly int X;
        public readonly int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public uint Size;
        public NativeRect Monitor;
        public NativeRect WorkArea;
        public uint Flags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct NativeRect
    {
        public readonly int Left;
        public readonly int Top;
        public readonly int Right;
        public readonly int Bottom;
    }
}
