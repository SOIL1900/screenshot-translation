using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ScreenshotTranslation.Core.Configuration;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using UserControl = System.Windows.Controls.UserControl;

namespace ScreenshotTranslation.App.Settings;

public partial class HotkeyRecorder : UserControl
{
    public static readonly DependencyProperty GestureProperty = DependencyProperty.Register(
        nameof(Gesture),
        typeof(HotkeyGesture),
        typeof(HotkeyRecorder),
        new FrameworkPropertyMetadata(
            HotkeyGesture.Default,
            FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            OnGestureChanged));

    public HotkeyRecorder()
    {
        InitializeComponent();
        UpdateDisplayText();
    }

    public HotkeyGesture Gesture
    {
        get => (HotkeyGesture)GetValue(GestureProperty);
        set => SetValue(GestureProperty, value);
    }

    private static void OnGestureChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        ((HotkeyRecorder)dependencyObject).UpdateDisplayText();
    }

    private void OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs eventArgs)
    {
        _ = Focus();
        eventArgs.Handled = true;
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs eventArgs)
    {
        var key = eventArgs.Key == Key.System ? eventArgs.SystemKey : eventArgs.Key;
        eventArgs.Handled = true;
        if (IsModifierKey(key))
        {
            DisplayTextBlock.Text = "请继续按下一个字母、数字或功能键";
            return;
        }

        var modifiers = GetModifiers(Keyboard.Modifiers);
        if (modifiers == HotkeyModifiers.None || key is Key.None or Key.ImeProcessed or Key.DeadCharProcessed)
        {
            DisplayTextBlock.Text = "快捷键需要修饰键和一个非修饰键";
            return;
        }

        var virtualKey = KeyInterop.VirtualKeyFromKey(key);
        if (virtualKey <= 0)
        {
            DisplayTextBlock.Text = "无法识别该按键，请尝试其他组合";
            return;
        }

        Gesture = new HotkeyGesture(modifiers, virtualKey);
    }

    private void UpdateDisplayText()
    {
        if (DisplayTextBlock is null)
        {
            return;
        }

        DisplayTextBlock.Text = FormatGesture(Gesture);
    }

    private static HotkeyModifiers GetModifiers(ModifierKeys modifiers)
    {
        var result = HotkeyModifiers.None;
        if (modifiers.HasFlag(ModifierKeys.Control))
        {
            result |= HotkeyModifiers.Control;
        }

        if (modifiers.HasFlag(ModifierKeys.Alt))
        {
            result |= HotkeyModifiers.Alt;
        }

        if (modifiers.HasFlag(ModifierKeys.Shift))
        {
            result |= HotkeyModifiers.Shift;
        }

        if (modifiers.HasFlag(ModifierKeys.Windows))
        {
            result |= HotkeyModifiers.Windows;
        }

        return result;
    }

    private static bool IsModifierKey(Key key) => key is
        Key.LeftCtrl or Key.RightCtrl or
        Key.LeftAlt or Key.RightAlt or
        Key.LeftShift or Key.RightShift or
        Key.LWin or Key.RWin;

    private static string FormatGesture(HotkeyGesture gesture)
    {
        var parts = new List<string>();
        if (gesture.Modifiers.HasFlag(HotkeyModifiers.Control))
        {
            parts.Add("Ctrl");
        }

        if (gesture.Modifiers.HasFlag(HotkeyModifiers.Alt))
        {
            parts.Add("Alt");
        }

        if (gesture.Modifiers.HasFlag(HotkeyModifiers.Shift))
        {
            parts.Add("Shift");
        }

        if (gesture.Modifiers.HasFlag(HotkeyModifiers.Windows))
        {
            parts.Add("Windows");
        }

        parts.Add(FormatVirtualKey(gesture.VirtualKey));
        return string.Join(" + ", parts);
    }

    private static string FormatVirtualKey(int virtualKey)
    {
        if (virtualKey is >= 0x41 and <= 0x5A)
        {
            return ((char)virtualKey).ToString();
        }

        if (virtualKey is >= 0x30 and <= 0x39)
        {
            return ((char)virtualKey).ToString();
        }

        return KeyInterop.KeyFromVirtualKey(virtualKey).ToString();
    }
}
