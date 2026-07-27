using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using ScreenshotTranslation.Core.Geometry;
using ScreenshotTranslation.Infrastructure.Windows;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using WpfMouseButtonEventArgs = System.Windows.Input.MouseButtonEventArgs;
using Window = System.Windows.Window;

namespace ScreenshotTranslation.App.Overlay;

public partial class CaptureOverlayWindow : Window
{
    private static readonly nint TopmostWindow = new(-1);
    private const uint SwpShowWindow = 0x0040;

    private readonly CapturedMonitorFrame _frame;
    private readonly OverlayViewModel _viewModel;
    private readonly nint _previousForegroundWindow;
    private readonly ForegroundWindowService _foregroundWindowService;
    private OverlayCoordinateMapper _coordinateMapper = new(96, 96);

    public CaptureOverlayWindow(
        CapturedMonitorFrame frame,
        OverlayViewModel viewModel,
        nint previousForegroundWindow,
        ForegroundWindowService foregroundWindowService)
    {
        ArgumentNullException.ThrowIfNull(frame);
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(foregroundWindowService);
        var localFrameBounds = new PixelRect(
            0,
            0,
            frame.Monitor.PhysicalBounds.Width,
            frame.Monitor.PhysicalBounds.Height);
        if (viewModel.ScreenBounds != localFrameBounds)
        {
            throw new ArgumentException(
                "The overlay view model must use monitor-local frame bounds.",
                nameof(viewModel));
        }

        InitializeComponent();

        _frame = frame;
        _viewModel = viewModel;
        _previousForegroundWindow = previousForegroundWindow;
        _foregroundWindowService = foregroundWindowService;
        DataContext = viewModel;

        SelectionSurface.FrozenFrame = DecodeFrozenFrame(frame.PngBytes);
        SelectionSurface.ViewModel = viewModel;
        SelectionSurface.PointerActionCompleted += OnPointerActionCompleted;
        SelectionSurface.OutsideClickRequested += OnOutsideClickRequested;
        SelectionSurface.RightClickRequested += OnRightClickRequested;
        _viewModel.CloseRequested += OnCloseRequested;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        ResultPanel.Loaded += OnResultPanelLoaded;
    }

    private void OnSourceInitialized(object? sender, EventArgs eventArgs)
    {
        var windowHandle = new WindowInteropHelper(this).Handle;
        var bounds = _frame.Monitor.PhysicalBounds;
        _coordinateMapper = OverlayCoordinateMapper.FromWindow(windowHandle);
        SelectionSurface.SetCoordinateMapper(_coordinateMapper);

        if (!SetWindowPos(
                windowHandle,
                TopmostWindow,
                bounds.X,
                bounds.Y,
                bounds.Width,
                bounds.Height,
                SwpShowWindow))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Unable to position the capture overlay.");
        }

        UpdateResultPanelPosition();
        _ = Activate();
        _ = SelectionSurface.Focus();
    }

    private async void OnPointerActionCompleted(object? sender, EventArgs eventArgs)
    {
        await _viewModel.CompletePointerActionAsync();
    }

    private void OnOutsideClickRequested(object? sender, EventArgs eventArgs) =>
        _viewModel.HandleOutsideClick();

    private void OnRightClickRequested(object? sender, EventArgs eventArgs) =>
        _viewModel.HandleRightClick();

    private void OnWindowKeyDown(object sender, KeyEventArgs eventArgs)
    {
        if (eventArgs.Key == Key.Escape)
        {
            eventArgs.Handled = true;
            _viewModel.HandleEscape();
        }
    }

    private void OnWindowPreviewMouseRightButtonDown(
        object sender,
        WpfMouseButtonEventArgs eventArgs)
    {
        eventArgs.Handled = true;
        _viewModel.HandleRightClick();
    }

    private void OnCloseRequested(object? sender, EventArgs eventArgs) => Close();

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.PropertyName == nameof(OverlayViewModel.Selection))
        {
            UpdateResultPanelPosition();
        }
    }

    private void OnResultPanelLoaded(object sender, RoutedEventArgs eventArgs) => UpdateResultPanelPosition();

    private void UpdateResultPanelPosition()
    {
        if (!_viewModel.HasValidSelection || _viewModel.Selection is not { } selection)
        {
            ResultPanel.Visibility = Visibility.Collapsed;
            return;
        }

        var screenDip = _coordinateMapper.ToDip(_viewModel.ScreenBounds);
        var selectionDip = _coordinateMapper.ToDip(selection);
        var panelWidthDip = Math.Min(
            screenDip.Width,
            Math.Clamp(
                selectionDip.Width,
                OverlayViewModel.PanelMinimumWidth,
                OverlayViewModel.PanelMaximumWidth));
        var panelHeightDip = Math.Min(screenDip.Height, OverlayViewModel.PanelHeight);
        ResultPanel.Width = panelWidthDip;
        ResultPanel.Height = panelHeightDip;

        var panelWidthPhysical = _coordinateMapper.DipLengthToPhysicalX(panelWidthDip);
        var panelHeightPhysical = _coordinateMapper.DipLengthToPhysicalY(panelHeightDip);
        var panelBounds = ResultPanelPlacement.Place(
            selection,
            panelWidthPhysical,
            panelHeightPhysical,
            _viewModel.ScreenBounds,
            OverlayViewModel.PanelGap);
        var panelBoundsDip = _coordinateMapper.ToDip(panelBounds);
        Canvas.SetLeft(ResultPanel, panelBoundsDip.X);
        Canvas.SetTop(ResultPanel, panelBoundsDip.Y);
        ResultPanel.Visibility = Visibility.Visible;
    }

    private void OnWindowClosed(object? sender, EventArgs eventArgs)
    {
        ResultPanel.Loaded -= OnResultPanelLoaded;
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _viewModel.CloseRequested -= OnCloseRequested;
        SelectionSurface.PointerActionCompleted -= OnPointerActionCompleted;
        SelectionSurface.OutsideClickRequested -= OnOutsideClickRequested;
        SelectionSurface.RightClickRequested -= OnRightClickRequested;
        SelectionSurface.ViewModel = null;
        _viewModel.CancelPending();
        _ = _foregroundWindowService.RestoreForegroundWindow(_previousForegroundWindow);
    }

    private static BitmapSource DecodeFrozenFrame(byte[] pngBytes)
    {
        using var stream = new MemoryStream(pngBytes, writable: false);
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.StreamSource = stream;
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(
        nint windowHandle,
        nint insertAfter,
        int x,
        int y,
        int width,
        int height,
        uint flags);
}
