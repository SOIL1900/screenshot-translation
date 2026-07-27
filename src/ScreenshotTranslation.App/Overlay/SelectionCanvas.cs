using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using ScreenshotTranslation.Core.Geometry;
using WpfBrush = System.Windows.Media.Brush;
using WpfBrushes = System.Windows.Media.Brushes;
using WpfCursors = System.Windows.Input.Cursors;
using WpfMouseEventArgs = System.Windows.Input.MouseEventArgs;
using WpfPen = System.Windows.Media.Pen;

namespace ScreenshotTranslation.App.Overlay;

public sealed class SelectionCanvas : FrameworkElement
{
    private const double HandleVisualDiameter = 10;
    private const double HandleHitDiameter = 18;
    private const double CursorRingRadius = 9;

    private OverlayViewModel? _viewModel;
    private OverlayCoordinateMapper _coordinateMapper = new(96, 96);
    private System.Windows.Point? _cursorPosition;

    public SelectionCanvas()
    {
        Cursor = WpfCursors.Cross;
        Focusable = true;
    }

    public event EventHandler? PointerActionCompleted;

    public event EventHandler? OutsideClickRequested;

    public event EventHandler? RightClickRequested;

    public ImageSource? FrozenFrame { get; set; }

    public OverlayViewModel? ViewModel
    {
        get => _viewModel;
        set
        {
            if (ReferenceEquals(_viewModel, value))
            {
                return;
            }

            if (_viewModel is not null)
            {
                _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            }

            _viewModel = value;
            if (_viewModel is not null)
            {
                _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            }

            InvalidateVisual();
        }
    }

    public void SetCoordinateMapper(OverlayCoordinateMapper coordinateMapper)
    {
        ArgumentNullException.ThrowIfNull(coordinateMapper);
        _coordinateMapper = coordinateMapper;
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);
        var canvasBounds = new Rect(0, 0, ActualWidth, ActualHeight);
        if (FrozenFrame is not null)
        {
            drawingContext.DrawImage(FrozenFrame, canvasBounds);
        }

        var maskBrush = FindBrush("Overlay.Brush.Mask", WpfBrushes.Black);
        if (ViewModel?.Selection is { } selection && ViewModel.HasValidSelection)
        {
            var selectionBounds = _coordinateMapper.ToDip(selection);
            var outsideGeometry = new GeometryGroup { FillRule = FillRule.EvenOdd };
            outsideGeometry.Children.Add(new RectangleGeometry(canvasBounds));
            outsideGeometry.Children.Add(new RectangleGeometry(selectionBounds));
            drawingContext.DrawGeometry(maskBrush, null, outsideGeometry);

            var accentBrush = FindBrush("Overlay.Brush.Accent", WpfBrushes.MediumPurple);
            var borderThickness = _coordinateMapper.PhysicalLengthToDipX(2);
            drawingContext.DrawRectangle(
                null,
                new WpfPen(accentBrush, borderThickness),
                selectionBounds);
            DrawHandles(drawingContext, selectionBounds, accentBrush);
        }
        else
        {
            drawingContext.DrawRectangle(maskBrush, null, canvasBounds);
        }

        if (_cursorPosition is { } cursorPosition)
        {
            drawingContext.DrawEllipse(
                null,
                new WpfPen(FindBrush("Overlay.Brush.CursorRing", WpfBrushes.White), 1.25),
                cursorPosition,
                CursorRingRadius,
                CursorRingRadius);
        }
    }

    protected override void OnMouseDown(MouseButtonEventArgs eventArgs)
    {
        base.OnMouseDown(eventArgs);
        _ = Focus();
        if (eventArgs.ChangedButton == MouseButton.Right)
        {
            eventArgs.Handled = true;
            RightClickRequested?.Invoke(this, EventArgs.Empty);
            return;
        }

        if (eventArgs.ChangedButton != MouseButton.Left || ViewModel is null)
        {
            return;
        }

        var dipPoint = eventArgs.GetPosition(this);
        var physicalPoint = _coordinateMapper.ToPhysical(dipPoint);
        if (ViewModel.HasValidSelection && ViewModel.Selection is { } selection)
        {
            var handle = HitTestHandle(dipPoint, _coordinateMapper.ToDip(selection));
            if (handle != ResizeHandle.None)
            {
                ViewModel.BeginResize(handle, physicalPoint);
            }
            else if (selection.Contains(physicalPoint))
            {
                ViewModel.BeginMove(physicalPoint);
            }
            else
            {
                OutsideClickRequested?.Invoke(this, EventArgs.Empty);
                eventArgs.Handled = true;
                return;
            }
        }
        else
        {
            ViewModel.BeginSelection(physicalPoint);
        }

        _ = CaptureMouse();
        eventArgs.Handled = true;
    }

    protected override void OnMouseMove(WpfMouseEventArgs eventArgs)
    {
        base.OnMouseMove(eventArgs);
        _cursorPosition = eventArgs.GetPosition(this);
        if (IsMouseCaptured && ViewModel is not null)
        {
            ViewModel.UpdatePointer(_coordinateMapper.ToPhysical(_cursorPosition.Value));
        }

        InvalidateVisual();
    }

    protected override void OnMouseUp(MouseButtonEventArgs eventArgs)
    {
        base.OnMouseUp(eventArgs);
        if (eventArgs.ChangedButton != MouseButton.Left || !IsMouseCaptured || ViewModel is null)
        {
            return;
        }

        ViewModel.UpdatePointer(_coordinateMapper.ToPhysical(eventArgs.GetPosition(this)));
        ReleaseMouseCapture();
        eventArgs.Handled = true;
        PointerActionCompleted?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnMouseLeave(WpfMouseEventArgs eventArgs)
    {
        base.OnMouseLeave(eventArgs);
        if (!IsMouseCaptured)
        {
            _cursorPosition = null;
            InvalidateVisual();
        }
    }

    private void DrawHandles(DrawingContext drawingContext, Rect selectionBounds, WpfBrush accentBrush)
    {
        var handleBrush = FindBrush("Overlay.Brush.Handle", WpfBrushes.White);
        var pen = new WpfPen(accentBrush, 1.5);
        foreach (var (_, center) in GetHandleCenters(selectionBounds))
        {
            drawingContext.DrawEllipse(
                handleBrush,
                pen,
                center,
                HandleVisualDiameter / 2,
                HandleVisualDiameter / 2);
        }
    }

    private static ResizeHandle HitTestHandle(System.Windows.Point pointer, Rect selectionBounds)
    {
        var radius = HandleHitDiameter / 2;
        foreach (var (handle, center) in GetHandleCenters(selectionBounds))
        {
            if (Math.Abs(pointer.X - center.X) <= radius &&
                Math.Abs(pointer.Y - center.Y) <= radius)
            {
                return handle;
            }
        }

        return ResizeHandle.None;
    }

    private static IReadOnlyList<(ResizeHandle Handle, System.Windows.Point Center)> GetHandleCenters(Rect bounds)
    {
        var centerX = bounds.Left + (bounds.Width / 2);
        var centerY = bounds.Top + (bounds.Height / 2);
        return
        [
            (ResizeHandle.TopLeft, bounds.TopLeft),
            (ResizeHandle.Top, new System.Windows.Point(centerX, bounds.Top)),
            (ResizeHandle.TopRight, bounds.TopRight),
            (ResizeHandle.Right, new System.Windows.Point(bounds.Right, centerY)),
            (ResizeHandle.BottomRight, bounds.BottomRight),
            (ResizeHandle.Bottom, new System.Windows.Point(centerX, bounds.Bottom)),
            (ResizeHandle.BottomLeft, bounds.BottomLeft),
            (ResizeHandle.Left, new System.Windows.Point(bounds.Left, centerY))
        ];
    }

    private WpfBrush FindBrush(string key, WpfBrush fallback) =>
        TryFindResource(key) as WpfBrush ?? fallback;

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.PropertyName is nameof(OverlayViewModel.Selection) or nameof(OverlayViewModel.PointerMode))
        {
            InvalidateVisual();
        }
    }
}
