using System.Windows.Input;
using ScreenshotTranslation.App.Settings;

namespace ScreenshotTranslation.App.Tests.Settings;

public sealed class HotkeyRecorderTests
{
    [Theory]
    [InlineData(ModifierKeys.None)]
    [InlineData(ModifierKeys.Shift)]
    public void Tab_and_shift_tab_remain_focus_navigation(ModifierKeys modifiers)
    {
        Assert.True(HotkeyRecorder.IsFocusNavigationInput(Key.Tab, modifiers));
    }

    [Fact]
    public void Modified_non_tab_key_remains_recordable()
    {
        Assert.False(HotkeyRecorder.IsFocusNavigationInput(
            Key.D,
            ModifierKeys.Control | ModifierKeys.Alt));
    }
}
