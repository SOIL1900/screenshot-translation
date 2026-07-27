using System.ComponentModel;
using System.Runtime.CompilerServices;
using ScreenshotTranslation.App.Settings;
using ScreenshotTranslation.Core.Configuration;
using ScreenshotTranslation.Core.Geometry;
using ScreenshotTranslation.Core.Translation;
using ScreenshotTranslation.Infrastructure.Windows;

namespace ScreenshotTranslation.App.Overlay;

public enum OverlayPointerMode
{
    Idle,
    Creating,
    Moving,
    Resizing
}

public enum OverlayTranslationState
{
    Idle,
    Loading,
    Success,
    NoText,
    Error
}

public sealed class OverlayViewModel : INotifyPropertyChanged
{
    public const int MinimumSelectionSize = 24;
    public const int PanelMinimumWidth = 420;
    public const int PanelMaximumWidth = 720;
    public const int PanelHeight = 360;
    public const int PanelGap = 12;

    private static readonly TimeSpan LoadingDelay = TimeSpan.FromMilliseconds(300);

    private readonly byte[] _frozenPng;
    private readonly ModelSettings _modelSettings;
    private readonly IPngCropService _cropService;
    private readonly IOverlayTranslationCoordinator _translationCoordinator;
    private readonly IOverlayClipboardService _clipboard;
    private readonly IOverlayDelay _delay;

    private PixelRect? _selection;
    private OverlayPointerMode _pointerMode;
    private PixelPoint _pointerStart;
    private PixelRect _pointerStartSelection;
    private ResizeHandle _resizeHandle;
    private byte[]? _currentCropPng;
    private string _screenshotTargetLanguage;
    private OverlayTranslationState _screenshotState;
    private string _statusMessage = "拖动鼠标选择需要翻译的区域。";
    private string? _screenshotTranslation;
    private string? _detectedSourceLanguage;
    private string? _detectedSourceLanguageCode;
    private bool _isScreenshotLoadingVisible;
    private string _replyInput = string.Empty;
    private string? _replyTargetLanguage;
    private string? _replyTranslation;
    private OverlayTranslationState _replyState;
    private string _replyStatusMessage = "完成截图翻译后可输入快捷回复。";
    private string? _clipboardError;
    private long _translationVersion;
    private bool _closeRequested;

    public OverlayViewModel(
        byte[] frozenPng,
        PixelRect screenBounds,
        PixelRect? existingSelection,
        string screenshotTargetLanguage,
        ModelSettings modelSettings,
        IPngCropService cropService,
        IOverlayTranslationCoordinator translationCoordinator,
        IOverlayClipboardService clipboard,
        IOverlayDelay delay)
    {
        ArgumentNullException.ThrowIfNull(frozenPng);
        ArgumentException.ThrowIfNullOrWhiteSpace(screenshotTargetLanguage);
        ArgumentNullException.ThrowIfNull(modelSettings);
        ArgumentNullException.ThrowIfNull(cropService);
        ArgumentNullException.ThrowIfNull(translationCoordinator);
        ArgumentNullException.ThrowIfNull(clipboard);
        ArgumentNullException.ThrowIfNull(delay);
        if (frozenPng.Length == 0)
        {
            throw new ArgumentException("The frozen screenshot cannot be empty.", nameof(frozenPng));
        }

        if (screenBounds.Width <= 0 || screenBounds.Height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(screenBounds), "Screen bounds must be positive.");
        }

        if (screenBounds.X != 0 || screenBounds.Y != 0)
        {
            throw new ArgumentException(
                "Screen bounds must use the frame-local origin (0, 0).",
                nameof(screenBounds));
        }

        _frozenPng = frozenPng.ToArray();
        ScreenBounds = screenBounds;
        _modelSettings = modelSettings;
        _cropService = cropService;
        _translationCoordinator = translationCoordinator;
        _clipboard = clipboard;
        _delay = delay;
        _screenshotTargetLanguage = screenshotTargetLanguage;
        Selection = existingSelection;

        RetryScreenshotCommand = new AsyncCommand(_ => RetryScreenshotAsync(), _ => CanRetryScreenshot);
        TranslateReplyCommand = new AsyncCommand(_ => TranslateReplyAsync(), _ => CanTranslateReply);
        CopyScreenshotCommand = new AsyncCommand(_ =>
        {
            CopyScreenshotTranslation();
            return Task.CompletedTask;
        }, _ => CanCopyScreenshotTranslation);
        CopyReplyCommand = new AsyncCommand(_ =>
        {
            CopyReplyTranslation();
            return Task.CompletedTask;
        }, _ => CanCopyReplyTranslation);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public event EventHandler? CloseRequested;

    public PixelRect ScreenBounds { get; }

    public IReadOnlyList<LanguageOption> Languages => LanguageCatalog.All;

    public AsyncCommand RetryScreenshotCommand { get; }

    public AsyncCommand TranslateReplyCommand { get; }

    public AsyncCommand CopyScreenshotCommand { get; }

    public AsyncCommand CopyReplyCommand { get; }

    public PixelRect? Selection
    {
        get => _selection;
        private set
        {
            if (!SetProperty(ref _selection, value))
            {
                return;
            }

            _currentCropPng = null;
            OnPropertyChanged(nameof(HasValidSelection));
        }
    }

    public bool HasValidSelection => IsValidSelection(Selection);

    public OverlayPointerMode PointerMode
    {
        get => _pointerMode;
        private set => SetProperty(ref _pointerMode, value);
    }

    public string ScreenshotTargetLanguage
    {
        get => _screenshotTargetLanguage;
        private set => SetProperty(ref _screenshotTargetLanguage, value);
    }

    public OverlayTranslationState ScreenshotState
    {
        get => _screenshotState;
        private set
        {
            if (SetProperty(ref _screenshotState, value))
            {
                OnPropertyChanged(nameof(CanRetryScreenshot));
                OnPropertyChanged(nameof(CanTranslateReply));
                RetryScreenshotCommand.NotifyCanExecuteChanged();
                TranslateReplyCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public string? ScreenshotTranslation
    {
        get => _screenshotTranslation;
        private set
        {
            if (SetProperty(ref _screenshotTranslation, value))
            {
                OnPropertyChanged(nameof(CanCopyScreenshotTranslation));
                CopyScreenshotCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public string? DetectedSourceLanguage
    {
        get => _detectedSourceLanguage;
        private set => SetProperty(ref _detectedSourceLanguage, value);
    }

    public string? DetectedSourceLanguageCode
    {
        get => _detectedSourceLanguageCode;
        private set => SetProperty(ref _detectedSourceLanguageCode, value);
    }

    public bool IsScreenshotLoadingVisible
    {
        get => _isScreenshotLoadingVisible;
        private set => SetProperty(ref _isScreenshotLoadingVisible, value);
    }

    public string ReplyInput
    {
        get => _replyInput;
        set
        {
            if (SetProperty(ref _replyInput, value))
            {
                InvalidateReplyResult();
                OnPropertyChanged(nameof(CanTranslateReply));
                TranslateReplyCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public string? ReplyTargetLanguage
    {
        get => _replyTargetLanguage;
        set
        {
            if (SetProperty(ref _replyTargetLanguage, value))
            {
                InvalidateReplyResult();
                OnPropertyChanged(nameof(CanTranslateReply));
                TranslateReplyCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public string? ReplyTranslation
    {
        get => _replyTranslation;
        private set
        {
            if (SetProperty(ref _replyTranslation, value))
            {
                OnPropertyChanged(nameof(CanCopyReplyTranslation));
                CopyReplyCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public OverlayTranslationState ReplyState
    {
        get => _replyState;
        private set => SetProperty(ref _replyState, value);
    }

    public string ReplyStatusMessage
    {
        get => _replyStatusMessage;
        private set => SetProperty(ref _replyStatusMessage, value);
    }

    public string? ClipboardError
    {
        get => _clipboardError;
        private set => SetProperty(ref _clipboardError, value);
    }

    public bool CanRetryScreenshot => HasValidSelection &&
        ScreenshotState is OverlayTranslationState.Error or OverlayTranslationState.NoText;

    public bool CanTranslateReply =>
        !string.IsNullOrWhiteSpace(ReplyInput) &&
        !string.IsNullOrWhiteSpace(ReplyTargetLanguage) &&
        ScreenshotState != OverlayTranslationState.Loading &&
        ReplyState != OverlayTranslationState.Loading;

    public bool CanCopyScreenshotTranslation => !string.IsNullOrWhiteSpace(ScreenshotTranslation);

    public bool CanCopyReplyTranslation => !string.IsNullOrWhiteSpace(ReplyTranslation);

    public void BeginSelection(PixelPoint start)
    {
        InvalidatePendingTranslation();
        PointerMode = OverlayPointerMode.Creating;
        _pointerStart = start;
        Selection = SelectionGeometry.Create(start, start, ScreenBounds);
        ClearScreenshotResult();
    }

    public void BeginMove(PixelPoint start)
    {
        if (!HasValidSelection)
        {
            return;
        }

        InvalidatePendingTranslation();
        PointerMode = OverlayPointerMode.Moving;
        _pointerStart = start;
        _pointerStartSelection = Selection!.Value;
    }

    public void BeginResize(ResizeHandle handle, PixelPoint start)
    {
        if (!HasValidSelection || handle == ResizeHandle.None)
        {
            return;
        }

        InvalidatePendingTranslation();
        PointerMode = OverlayPointerMode.Resizing;
        _resizeHandle = handle;
        _pointerStart = start;
        _pointerStartSelection = Selection!.Value;
    }

    public void UpdatePointer(PixelPoint current)
    {
        Selection = PointerMode switch
        {
            OverlayPointerMode.Creating => SelectionGeometry.Create(_pointerStart, current, ScreenBounds),
            OverlayPointerMode.Moving => SelectionGeometry.Move(
                _pointerStartSelection,
                current.X - _pointerStart.X,
                current.Y - _pointerStart.Y,
                ScreenBounds),
            OverlayPointerMode.Resizing => SelectionGeometry.Resize(
                _pointerStartSelection,
                _resizeHandle,
                current.X - _pointerStart.X,
                current.Y - _pointerStart.Y,
                ScreenBounds,
                MinimumSelectionSize),
            _ => Selection
        };
    }

    public async Task CompletePointerActionAsync()
    {
        var completedMode = PointerMode;
        PointerMode = OverlayPointerMode.Idle;
        if (completedMode == OverlayPointerMode.Idle)
        {
            return;
        }

        if (!HasValidSelection)
        {
            Selection = null;
            RequestClose();
            return;
        }

        await TranslateCurrentSelectionAsync(forceRecrop: true);
    }

    public async Task ChangeScreenshotTargetLanguageAsync(string targetLanguageCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetLanguageCode);
        if (string.Equals(ScreenshotTargetLanguage, targetLanguageCode, StringComparison.Ordinal))
        {
            return;
        }

        ScreenshotTargetLanguage = targetLanguageCode;
        if (HasValidSelection)
        {
            await TranslateCurrentSelectionAsync(forceRecrop: false);
        }
    }

    public Task RetryScreenshotAsync() =>
        HasValidSelection
            ? TranslateCurrentSelectionAsync(forceRecrop: false)
            : Task.CompletedTask;

    public async Task TranslateReplyAsync()
    {
        if (!CanTranslateReply)
        {
            ReplyStatusMessage = "请先输入回复并选择回复目标语言。";
            return;
        }

        InvalidateReplyResult();
        var version = ++_translationVersion;
        ReplyState = OverlayTranslationState.Loading;
        ReplyStatusMessage = "正在翻译回复…";
        ReplyTranslation = null;
        try
        {
            var result = await _translationCoordinator.TranslateReplyAsync(
                ReplyInput,
                ReplyTargetLanguage!,
                _modelSettings,
                CancellationToken.None);
            if (version != _translationVersion || result is null)
            {
                return;
            }

            ReplyTranslation = result.Translation;
            ReplyState = OverlayTranslationState.Success;
            ReplyStatusMessage = "回复翻译完成。";
        }
        catch (TranslationClientException exception)
        {
            if (version != _translationVersion)
            {
                return;
            }

            ReplyState = OverlayTranslationState.Error;
            ReplyStatusMessage = GetTranslationErrorMessage(exception.Code, "回复");
        }
        catch (Exception)
        {
            if (version != _translationVersion)
            {
                return;
            }

            ReplyState = OverlayTranslationState.Error;
            ReplyStatusMessage = "回复翻译失败，请检查网络后重试。";
        }
    }

    public void CopyScreenshotTranslation() => CopyAndClose(ScreenshotTranslation);

    public void CopyReplyTranslation() => CopyAndClose(ReplyTranslation);

    public void HandleEscape() => RequestClose();

    public void HandleOutsideClick() => RequestClose();

    public void HandleRightClick() => RequestClose();

    public void CancelPending()
    {
        _translationVersion++;
        _translationCoordinator.Cancel();
    }

    private async Task TranslateCurrentSelectionAsync(bool forceRecrop)
    {
        if (!HasValidSelection)
        {
            return;
        }

        InvalidateReplyResult();
        var version = ++_translationVersion;
        var requiresCrop = forceRecrop || _currentCropPng is null;
        ScreenshotState = OverlayTranslationState.Loading;
        StatusMessage = "正在翻译截图…";
        IsScreenshotLoadingVisible = false;
        ScreenshotTranslation = null;
        ClipboardError = null;
        using var delayCancellation = new CancellationTokenSource();
        try
        {
            if (requiresCrop)
            {
                _currentCropPng = _cropService.Crop(_frozenPng, Selection!.Value);
            }

            var translationTask = _translationCoordinator.TranslateScreenshotAsync(
                _currentCropPng,
                ScreenshotTargetLanguage,
                _modelSettings,
                CancellationToken.None);
            var delayTask = _delay.DelayAsync(LoadingDelay, delayCancellation.Token);
            var firstCompletion = await Task.WhenAny(translationTask, delayTask);
            if (firstCompletion == delayTask && !translationTask.IsCompleted && version == _translationVersion)
            {
                IsScreenshotLoadingVisible = true;
            }

            var result = await translationTask;
            if (version != _translationVersion || result is null)
            {
                return;
            }

            if (result.Status == TranslationResultStatus.NoText)
            {
                ScreenshotState = OverlayTranslationState.NoText;
                StatusMessage = "未识别到可翻译内容，请调整选区后重试。";
                ReplyTargetLanguage = null;
                ReplyStatusMessage = "识别到源语言后才能设置默认回复语言。";
                return;
            }

            ScreenshotTranslation = result.Translation;
            DetectedSourceLanguage = result.SourceLanguage;
            DetectedSourceLanguageCode = NullIfWhiteSpace(result.SourceLanguageCode);
            ReplyTargetLanguage = DetectedSourceLanguageCode;
            ReplyStatusMessage = ReplyTargetLanguage is null
                ? "未检测到源语言，请选择回复目标语言。"
                : "按 Enter 或点击翻译，将回复转换为截图源语言。";
            ScreenshotState = OverlayTranslationState.Success;
            StatusMessage = "截图翻译完成。";
        }
        catch (TranslationClientException exception)
        {
            if (version != _translationVersion)
            {
                return;
            }

            ScreenshotState = OverlayTranslationState.Error;
            StatusMessage = GetTranslationErrorMessage(exception.Code, "截图");
        }
        catch (Exception)
        {
            if (version != _translationVersion)
            {
                return;
            }

            ScreenshotState = OverlayTranslationState.Error;
            StatusMessage = requiresCrop && _currentCropPng is null
                ? "无法处理当前截图，请调整选区后重试。"
                : "截图翻译失败，请检查网络后重试。";
        }
        finally
        {
            delayCancellation.Cancel();
            if (version == _translationVersion)
            {
                IsScreenshotLoadingVisible = false;
            }
        }
    }

    private void CopyAndClose(string? visibleText)
    {
        if (string.IsNullOrWhiteSpace(visibleText))
        {
            return;
        }

        ClipboardError = null;
        try
        {
            _clipboard.SetText(visibleText);
            StatusMessage = "已复制。";
            RequestClose();
        }
        catch (Exception)
        {
            ClipboardError = "复制失败，剪贴板可能正被占用，请重试。";
        }
    }

    private void RequestClose()
    {
        if (_closeRequested)
        {
            return;
        }

        _closeRequested = true;
        CancelPending();
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    private void InvalidatePendingTranslation()
    {
        _translationVersion++;
        if (ScreenshotState == OverlayTranslationState.Loading || ReplyState == OverlayTranslationState.Loading)
        {
            _translationCoordinator.Cancel();
        }
    }

    private void InvalidateReplyResult()
    {
        if (ReplyState == OverlayTranslationState.Loading)
        {
            _translationVersion++;
            _translationCoordinator.Cancel();
        }

        ReplyState = OverlayTranslationState.Idle;
        ReplyTranslation = null;
        ReplyStatusMessage = string.IsNullOrWhiteSpace(ReplyTargetLanguage)
            ? "请选择回复目标语言。"
            : "按 Enter 或点击翻译，将回复转换为所选语言。";
    }

    private void ClearScreenshotResult()
    {
        ScreenshotState = OverlayTranslationState.Idle;
        StatusMessage = "松开鼠标后开始翻译。";
        ScreenshotTranslation = null;
        DetectedSourceLanguage = null;
        DetectedSourceLanguageCode = null;
        ReplyTargetLanguage = null;
        ReplyTranslation = null;
    }

    private static bool IsValidSelection(PixelRect? selection) =>
        selection is { Width: >= MinimumSelectionSize, Height: >= MinimumSelectionSize };

    private static string? NullIfWhiteSpace(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;

    private static string GetTranslationErrorMessage(TranslationErrorCode code, string operation) => code switch
    {
        TranslationErrorCode.Unauthorized => $"{operation}翻译失败，请检查 API Key、模型名称和服务权限后重试。",
        TranslationErrorCode.RateLimited => $"{operation}翻译受限，请稍后重试或检查额度。",
        TranslationErrorCode.Timeout => $"{operation}翻译超时，请检查网络后重试。",
        TranslationErrorCode.ServiceUnavailable => $"模型服务暂时不可用，请稍后重试{operation}翻译。",
        TranslationErrorCode.InvalidResponse => $"模型响应格式异常，请检查模型配置后重试。",
        _ => $"{operation}翻译失败，请检查网络后重试。"
    };

    private bool SetProperty<T>(
        ref T field,
        T value,
        [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
