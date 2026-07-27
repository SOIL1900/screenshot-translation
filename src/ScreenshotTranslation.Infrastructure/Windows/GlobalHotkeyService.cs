using System.Runtime.InteropServices;
using System.Windows.Interop;
using ScreenshotTranslation.Core.Configuration;

namespace ScreenshotTranslation.Infrastructure.Windows;

public sealed record HotkeyRegistrationResult(bool Succeeded, string? ErrorMessage)
{
    public static HotkeyRegistrationResult Success { get; } = new(true, null);
}

internal interface IGlobalHotkeyNativeMethods
{
    bool RegisterHotKey(
        nint windowHandle,
        int identifier,
        uint modifiers,
        uint virtualKey);

    bool UnregisterHotKey(nint windowHandle, int identifier);
}

public sealed class GlobalHotkeyService : IDisposable
{
    private const int WmHotkey = 0x0312;
    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;
    private const uint ModShift = 0x0004;
    private const uint ModWindows = 0x0008;
    private const uint ModNoRepeat = 0x4000;
    private const int FirstHotkeyId = 0x5343;
    private const int SecondHotkeyId = 0x5344;
    private static readonly nint MessageOnlyWindow = new(-3);

    private readonly IGlobalHotkeyNativeMethods _nativeMethods;
    private readonly HwndSource _window;
    private int? _registeredHotkeyId;
    private HotkeyGesture? _registeredGesture;
    private bool _disposed;

    public GlobalHotkeyService()
        : this(Win32GlobalHotkeyNativeMethods.Instance)
    {
    }

    internal GlobalHotkeyService(IGlobalHotkeyNativeMethods nativeMethods)
    {
        ArgumentNullException.ThrowIfNull(nativeMethods);
        _nativeMethods = nativeMethods;

        var parameters = new HwndSourceParameters("ScreenshotTranslation.GlobalHotkey")
        {
            ParentWindow = MessageOnlyWindow,
            WindowStyle = 0,
            Width = 0,
            Height = 0
        };
        _window = new HwndSource(parameters);
        _window.AddHook(WindowProcedure);
    }

    public event EventHandler? Pressed;

    public HotkeyRegistrationResult TryRegister(HotkeyGesture gesture)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(gesture);

        if (gesture == _registeredGesture)
        {
            return HotkeyRegistrationResult.Success;
        }

        if (!TryMapModifiers(gesture.Modifiers, out var modifiers) ||
            gesture.VirtualKey is <= 0 or > 0xFF)
        {
            return new HotkeyRegistrationResult(false, "快捷键组合无效，请选择其他组合。");
        }

        var candidateId = _registeredHotkeyId == FirstHotkeyId
            ? SecondHotkeyId
            : FirstHotkeyId;
        if (!_nativeMethods.RegisterHotKey(
                _window.Handle,
                candidateId,
                modifiers | ModNoRepeat,
                (uint)gesture.VirtualKey))
        {
            return new HotkeyRegistrationResult(false, "该快捷键已被其他应用占用，请选择其他组合。");
        }

        if (_registeredHotkeyId is { } previousId)
        {
            if (!_nativeMethods.UnregisterHotKey(_window.Handle, previousId))
            {
                _ = _nativeMethods.UnregisterHotKey(_window.Handle, candidateId);
                return new HotkeyRegistrationResult(false, "无法更新快捷键，请重试或选择其他组合。");
            }
        }

        _registeredHotkeyId = candidateId;
        _registeredGesture = gesture;
        return HotkeyRegistrationResult.Success;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_registeredHotkeyId is { } hotkeyId)
        {
            _ = _nativeMethods.UnregisterHotKey(_window.Handle, hotkeyId);
            _registeredHotkeyId = null;
            _registeredGesture = null;
        }

        _window.RemoveHook(WindowProcedure);
        _window.Dispose();
    }

    private nint WindowProcedure(
        nint windowHandle,
        int message,
        nint wordParameter,
        nint longParameter,
        ref bool handled)
    {
        if (message == WmHotkey &&
            _registeredHotkeyId is { } hotkeyId &&
            wordParameter == new nint(hotkeyId))
        {
            handled = true;
            Pressed?.Invoke(this, EventArgs.Empty);
        }

        return nint.Zero;
    }

    private static bool TryMapModifiers(HotkeyModifiers modifiers, out uint nativeModifiers)
    {
        const HotkeyModifiers knownModifiers =
            HotkeyModifiers.Alt |
            HotkeyModifiers.Control |
            HotkeyModifiers.Shift |
            HotkeyModifiers.Windows;

        if (modifiers == HotkeyModifiers.None || (modifiers & ~knownModifiers) != 0)
        {
            nativeModifiers = 0;
            return false;
        }

        nativeModifiers = 0;
        if (modifiers.HasFlag(HotkeyModifiers.Alt))
        {
            nativeModifiers |= ModAlt;
        }

        if (modifiers.HasFlag(HotkeyModifiers.Control))
        {
            nativeModifiers |= ModControl;
        }

        if (modifiers.HasFlag(HotkeyModifiers.Shift))
        {
            nativeModifiers |= ModShift;
        }

        if (modifiers.HasFlag(HotkeyModifiers.Windows))
        {
            nativeModifiers |= ModWindows;
        }

        return true;
    }

    private sealed class Win32GlobalHotkeyNativeMethods : IGlobalHotkeyNativeMethods
    {
        public static Win32GlobalHotkeyNativeMethods Instance { get; } = new();

        public bool RegisterHotKey(
            nint windowHandle,
            int identifier,
            uint modifiers,
            uint virtualKey) =>
            NativeRegisterHotKey(windowHandle, identifier, modifiers, virtualKey);

        public bool UnregisterHotKey(nint windowHandle, int identifier) =>
            NativeUnregisterHotKey(windowHandle, identifier);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool NativeRegisterHotKey(
            nint windowHandle,
            int identifier,
            uint modifiers,
            uint virtualKey);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool NativeUnregisterHotKey(nint windowHandle, int identifier);
    }
}
