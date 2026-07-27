using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace ScreenshotTranslation.App.Settings;

public partial class SettingsWindow : Window
{
    private readonly SettingsViewModel _viewModel;

    public SettingsWindow(SettingsViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        _viewModel.FocusRequested += OnFocusRequested;
        Closed += OnClosed;
    }

    private void OnNavigationSelectionChanged(object sender, SelectionChangedEventArgs eventArgs)
    {
        if (GeneralView is null || ModelView is null || AboutView is null)
        {
            return;
        }

        GeneralView.Visibility = NavigationList.SelectedIndex == 0 ? Visibility.Visible : Visibility.Collapsed;
        ModelView.Visibility = NavigationList.SelectedIndex == 1 ? Visibility.Visible : Visibility.Collapsed;
        AboutView.Visibility = NavigationList.SelectedIndex == 2 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnFocusRequested(object? sender, FocusRequestedEventArgs eventArgs)
    {
        NavigationList.SelectedIndex = eventArgs.FieldName.StartsWith("Model.", StringComparison.Ordinal)
            ? 1
            : 0;
        _ = Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
        {
            var target = FindElementByTag(this, eventArgs.FieldName);
            _ = target?.Focus();
        }));
    }

    private void OnClosed(object? sender, EventArgs eventArgs)
    {
        _viewModel.FocusRequested -= OnFocusRequested;
        Closed -= OnClosed;
    }

    private static FrameworkElement? FindElementByTag(DependencyObject parent, string tag)
    {
        var childCount = VisualTreeHelper.GetChildrenCount(parent);
        for (var index = 0; index < childCount; index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            if (child is FrameworkElement { Tag: string childTag } element &&
                string.Equals(childTag, tag, StringComparison.Ordinal))
            {
                return element;
            }

            var descendant = FindElementByTag(child, tag);
            if (descendant is not null)
            {
                return descendant;
            }
        }

        return null;
    }
}
