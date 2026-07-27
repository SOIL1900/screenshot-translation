namespace ScreenshotTranslation.Core.Configuration;

[Flags]
public enum HotkeyModifiers
{
    None = 0,
    Alt = 1,
    Control = 2,
    Shift = 4,
    Windows = 8
}

public sealed record HotkeyGesture(HotkeyModifiers Modifiers, int VirtualKey)
{
    public static HotkeyGesture Default { get; } = new(HotkeyModifiers.Control | HotkeyModifiers.Alt, 0x44);
}
