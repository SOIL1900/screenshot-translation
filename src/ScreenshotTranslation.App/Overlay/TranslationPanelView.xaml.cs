using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using WpfKeyEventArgs = System.Windows.Input.KeyEventArgs;
using UserControl = System.Windows.Controls.UserControl;

namespace ScreenshotTranslation.App.Overlay;

public partial class TranslationPanelView : UserControl
{
    private bool _isImeComposing;

    public TranslationPanelView()
    {
        InitializeComponent();
        TextCompositionManager.AddPreviewTextInputStartHandler(ReplyInput, OnTextInputStart);
        TextCompositionManager.AddPreviewTextInputUpdateHandler(ReplyInput, OnTextInputUpdate);
        TextCompositionManager.AddPreviewTextInputHandler(ReplyInput, OnTextInputCompleted);
    }

    private async void OnScreenshotTargetLanguageChanged(object sender, SelectionChangedEventArgs eventArgs)
    {
        if (!IsLoaded ||
            DataContext is not OverlayViewModel viewModel ||
            ScreenshotTargetLanguageInput.SelectedValue is not string targetLanguageCode)
        {
            return;
        }

        await viewModel.ChangeScreenshotTargetLanguageAsync(targetLanguageCode);
    }

    private async void OnReplyInputPreviewKeyDown(object sender, WpfKeyEventArgs eventArgs)
    {
        if (eventArgs.Key != Key.Enter || _isImeComposing || DataContext is not OverlayViewModel viewModel)
        {
            return;
        }

        eventArgs.Handled = true;
        await viewModel.TranslateReplyAsync();
    }

    private void OnTextInputStart(object sender, TextCompositionEventArgs eventArgs) =>
        _isImeComposing = true;

    private void OnTextInputUpdate(object sender, TextCompositionEventArgs eventArgs) =>
        _isImeComposing = true;

    private void OnTextInputCompleted(object sender, TextCompositionEventArgs eventArgs)
    {
        _ = Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() => _isImeComposing = false));
    }
}
