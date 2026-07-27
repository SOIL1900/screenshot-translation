using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using UserControl = System.Windows.Controls.UserControl;

namespace ScreenshotTranslation.App.Settings;

public partial class ModelSettingsView : UserControl
{
    public ModelSettingsView()
    {
        InitializeComponent();
    }

    private void OnFieldLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs eventArgs)
    {
        if (DataContext is SettingsViewModel viewModel &&
            sender is FrameworkElement { Tag: string fieldName })
        {
            _ = viewModel.ValidateField(fieldName);
        }
    }
}
