using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using ScreenshotTranslation.App.Services;
using ScreenshotTranslation.Core.Abstractions;
using ScreenshotTranslation.Core.Configuration;
using ScreenshotTranslation.Core.Translation;

namespace ScreenshotTranslation.App.Settings;

public enum ConnectionTestState
{
    Idle,
    Testing,
    Success,
    Error
}

public sealed class SettingsViewModel : INotifyPropertyChanged
{
    private static readonly TimeSpan ConnectionLoadingDelay = TimeSpan.FromMilliseconds(300);

    private readonly ISettingsStore _settingsStore;
    private readonly IHotkeyRegistrationService _hotkeyService;
    private readonly IStartupRegistrationService _startupService;
    private readonly IThemeService _themeService;
    private readonly ITranslationClient _translationClient;
    private readonly ISettingsDelay _delay;
    private readonly Dictionary<string, string> _errors = new(StringComparer.Ordinal);

    private AppSettings _activeSettings;
    private HotkeyGesture _captureHotkey;
    private string _defaultTargetLanguage;
    private bool _runAtStartup;
    private AppTheme _theme;
    private string _baseUrl;
    private string _apiKey;
    private string _modelName;
    private bool _enableThinking;
    private double _temperature;
    private string _temperatureText;
    private int _maxOutputTokens;
    private string _maxOutputTokensText;
    private int _requestTimeoutSeconds;
    private string _requestTimeoutSecondsText;
    private string _extraParametersJson;
    private bool _isDirty;
    private bool _isSaving;
    private bool _isConnectionTestRunning;
    private bool _isConnectionTestLoading;
    private ConnectionTestState _connectionTestState;
    private string _connectionTestMessage = string.Empty;
    private string? _pageError;
    private string? _saveStatusMessage;
    private string? _firstInvalidField;

    public SettingsViewModel(
        AppSettings activeSettings,
        ISettingsStore settingsStore,
        IHotkeyRegistrationService hotkeyService,
        IStartupRegistrationService startupService,
        IThemeService themeService,
        ITranslationClient translationClient,
        ISettingsDelay delay)
    {
        ArgumentNullException.ThrowIfNull(activeSettings);
        ArgumentNullException.ThrowIfNull(settingsStore);
        ArgumentNullException.ThrowIfNull(hotkeyService);
        ArgumentNullException.ThrowIfNull(startupService);
        ArgumentNullException.ThrowIfNull(themeService);
        ArgumentNullException.ThrowIfNull(translationClient);
        ArgumentNullException.ThrowIfNull(delay);

        _activeSettings = activeSettings;
        _settingsStore = settingsStore;
        _hotkeyService = hotkeyService;
        _startupService = startupService;
        _themeService = themeService;
        _translationClient = translationClient;
        _delay = delay;

        _captureHotkey = activeSettings.General.CaptureHotkey;
        _defaultTargetLanguage = activeSettings.General.DefaultTargetLanguage;
        _runAtStartup = activeSettings.General.RunAtStartup;
        _theme = activeSettings.General.Theme;
        _baseUrl = activeSettings.Model.BaseUrl;
        _apiKey = activeSettings.Model.ApiKey;
        _modelName = activeSettings.Model.ModelName;
        _enableThinking = activeSettings.Model.EnableThinking;
        _temperature = activeSettings.Model.Temperature;
        _temperatureText = FormatNumber(_temperature);
        _maxOutputTokens = activeSettings.Model.MaxOutputTokens;
        _maxOutputTokensText = _maxOutputTokens.ToString(CultureInfo.InvariantCulture);
        _requestTimeoutSeconds = activeSettings.Model.RequestTimeoutSeconds;
        _requestTimeoutSecondsText = _requestTimeoutSeconds.ToString(CultureInfo.InvariantCulture);
        _extraParametersJson = activeSettings.Model.ExtraParametersJson;

        SaveCommand = new AsyncCommand(_ => SaveAsync(), _ => IsDirty && !IsSaving);
        TestConnectionCommand = new AsyncCommand(
            _ => TestConnectionAsync(),
            _ => !IsConnectionTestRunning);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public event EventHandler<FocusRequestedEventArgs>? FocusRequested;

    public AsyncCommand SaveCommand { get; }

    public AsyncCommand TestConnectionCommand { get; }

    public IReadOnlyList<LanguageOption> Languages => LanguageCatalog.All;

    public IReadOnlyList<AppTheme> Themes { get; } = Enum.GetValues<AppTheme>();

    public IReadOnlyList<ThemeOption> ThemeOptions { get; } =
    [
        new(AppTheme.System, "跟随系统"),
        new(AppTheme.Light, "浅色"),
        new(AppTheme.Dark, "深色")
    ];

    public IReadOnlyDictionary<string, string> Errors => _errors;

    public string ApplicationVersion { get; } =
        typeof(SettingsViewModel).Assembly.GetName().Version?.ToString(3) ?? "开发版本";

    public HotkeyGesture CaptureHotkey
    {
        get => _captureHotkey;
        set => SetEditableProperty(ref _captureHotkey, value, "General.CaptureHotkey");
    }

    public string DefaultTargetLanguage
    {
        get => _defaultTargetLanguage;
        set => SetEditableProperty(ref _defaultTargetLanguage, value, "General.DefaultTargetLanguage");
    }

    public bool RunAtStartup
    {
        get => _runAtStartup;
        set => SetEditableProperty(ref _runAtStartup, value, "General.RunAtStartup");
    }

    public AppTheme Theme
    {
        get => _theme;
        set => SetEditableProperty(ref _theme, value, "General.Theme");
    }

    public string BaseUrl
    {
        get => _baseUrl;
        set => SetEditableProperty(ref _baseUrl, value, "Model.BaseUrl");
    }

    public string ApiKey
    {
        get => _apiKey;
        set => SetEditableProperty(ref _apiKey, value, "Model.ApiKey");
    }

    public string ModelName
    {
        get => _modelName;
        set => SetEditableProperty(ref _modelName, value, "Model.ModelName");
    }

    public bool EnableThinking
    {
        get => _enableThinking;
        set => SetEditableProperty(ref _enableThinking, value, "Model.EnableThinking");
    }

    public double Temperature
    {
        get => _temperature;
        set
        {
            if (!SetProperty(ref _temperature, value))
            {
                return;
            }

            _temperatureText = FormatNumber(value);
            OnPropertyChanged(nameof(TemperatureText));
            EditablePropertyChanged("Model.Temperature");
        }
    }

    public string TemperatureText
    {
        get => _temperatureText;
        set
        {
            if (!SetProperty(ref _temperatureText, value))
            {
                return;
            }

            if (TryParseDouble(value, out var parsed))
            {
                _temperature = parsed;
                OnPropertyChanged(nameof(Temperature));
            }

            EditablePropertyChanged("Model.Temperature");
        }
    }

    public int MaxOutputTokens
    {
        get => _maxOutputTokens;
        set
        {
            if (!SetProperty(ref _maxOutputTokens, value))
            {
                return;
            }

            _maxOutputTokensText = value.ToString(CultureInfo.InvariantCulture);
            OnPropertyChanged(nameof(MaxOutputTokensText));
            EditablePropertyChanged("Model.MaxOutputTokens");
        }
    }

    public string MaxOutputTokensText
    {
        get => _maxOutputTokensText;
        set
        {
            if (!SetProperty(ref _maxOutputTokensText, value))
            {
                return;
            }

            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            {
                _maxOutputTokens = parsed;
                OnPropertyChanged(nameof(MaxOutputTokens));
            }

            EditablePropertyChanged("Model.MaxOutputTokens");
        }
    }

    public int RequestTimeoutSeconds
    {
        get => _requestTimeoutSeconds;
        set
        {
            if (!SetProperty(ref _requestTimeoutSeconds, value))
            {
                return;
            }

            _requestTimeoutSecondsText = value.ToString(CultureInfo.InvariantCulture);
            OnPropertyChanged(nameof(RequestTimeoutSecondsText));
            EditablePropertyChanged("Model.RequestTimeoutSeconds");
        }
    }

    public string RequestTimeoutSecondsText
    {
        get => _requestTimeoutSecondsText;
        set
        {
            if (!SetProperty(ref _requestTimeoutSecondsText, value))
            {
                return;
            }

            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            {
                _requestTimeoutSeconds = parsed;
                OnPropertyChanged(nameof(RequestTimeoutSeconds));
            }

            EditablePropertyChanged("Model.RequestTimeoutSeconds");
        }
    }

    public string ExtraParametersJson
    {
        get => _extraParametersJson;
        set => SetEditableProperty(ref _extraParametersJson, value, "Model.ExtraParametersJson");
    }

    public bool IsDirty
    {
        get => _isDirty;
        private set
        {
            if (SetProperty(ref _isDirty, value))
            {
                SaveCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public bool IsSaving
    {
        get => _isSaving;
        private set
        {
            if (SetProperty(ref _isSaving, value))
            {
                SaveCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public bool IsConnectionTestRunning
    {
        get => _isConnectionTestRunning;
        private set
        {
            if (SetProperty(ref _isConnectionTestRunning, value))
            {
                TestConnectionCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public bool IsConnectionTestLoading
    {
        get => _isConnectionTestLoading;
        private set => SetProperty(ref _isConnectionTestLoading, value);
    }

    public ConnectionTestState ConnectionTestState
    {
        get => _connectionTestState;
        private set => SetProperty(ref _connectionTestState, value);
    }

    public string ConnectionTestMessage
    {
        get => _connectionTestMessage;
        private set => SetProperty(ref _connectionTestMessage, value);
    }

    public string? PageError
    {
        get => _pageError;
        private set => SetProperty(ref _pageError, value);
    }

    public string? SaveStatusMessage
    {
        get => _saveStatusMessage;
        private set => SetProperty(ref _saveStatusMessage, value);
    }

    public string? FirstInvalidField
    {
        get => _firstInvalidField;
        private set => SetProperty(ref _firstInvalidField, value);
    }

    public bool ValidateField(string fieldName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fieldName);
        var normalizedField = NormalizeValidationField(fieldName);
        _errors.Remove(normalizedField);

        foreach (var issue in SettingsValidator.Validate(BuildSettings()))
        {
            var issueField = NormalizeValidationField(issue.Field);
            if (string.Equals(issueField, normalizedField, StringComparison.Ordinal))
            {
                _errors[issueField] = GetLocalizedValidationMessage(issue);
                break;
            }
        }

        PublishErrors();
        return !_errors.ContainsKey(normalizedField);
    }

    private async Task SaveAsync()
    {
        IsSaving = true;
        PageError = null;
        SaveStatusMessage = null;
        try
        {
            if (!ValidateAll())
            {
                RequestFirstInvalidFieldFocus();
                return;
            }

            var candidate = BuildSettings();
            var hotkeyResult = _hotkeyService.TryRegister(candidate.General.CaptureHotkey);
            if (!hotkeyResult.Succeeded)
            {
                _errors["General.CaptureHotkey"] = "该快捷键已被其他程序占用";
                PublishErrors();
                RequestFirstInvalidFieldFocus();
                return;
            }

            var persisted = false;
            try
            {
                _startupService.SetEnabled(candidate.General.RunAtStartup);
                await _settingsStore.SaveAsync(candidate, CancellationToken.None);
                persisted = true;
                _themeService.Apply(candidate.General.Theme);
                _activeSettings = candidate;
                UpdateDirtyState();
                SaveStatusMessage = "设置已保存。";
            }
            catch (Exception)
            {
                if (!persisted)
                {
                    RollBackPrePersistenceSideEffects();
                    PageError = "保存失败。请检查本机权限后重试，原设置仍然有效。";
                }
                else
                {
                    _activeSettings = candidate;
                    UpdateDirtyState();
                    PageError = "设置已保存，但主题未能立即应用。请重启应用后重试。";
                }
            }
        }
        finally
        {
            IsSaving = false;
        }
    }

    private async Task TestConnectionAsync()
    {
        PageError = null;
        if (!ValidateModelFields())
        {
            ConnectionTestState = ConnectionTestState.Error;
            ConnectionTestMessage = "请先修正模型配置中的字段错误，再测试连接。";
            RequestFirstInvalidFieldFocus();
            return;
        }

        IsConnectionTestRunning = true;
        IsConnectionTestLoading = false;
        ConnectionTestState = ConnectionTestState.Idle;
        ConnectionTestMessage = string.Empty;
        using var delayCancellation = new CancellationTokenSource();
        try
        {
            var connectionTask = _translationClient.TestConnectionAsync(
                BuildSettings().Model,
                CancellationToken.None);
            var delayTask = _delay.DelayAsync(ConnectionLoadingDelay, delayCancellation.Token);
            var firstCompletion = await Task.WhenAny(connectionTask, delayTask);
            if (firstCompletion == delayTask && !connectionTask.IsCompleted)
            {
                IsConnectionTestLoading = true;
                ConnectionTestState = ConnectionTestState.Testing;
                ConnectionTestMessage = "正在测试连接…";
            }

            await connectionTask;
            ConnectionTestState = ConnectionTestState.Success;
            ConnectionTestMessage = "连接成功，可以使用当前模型配置。";
        }
        catch (TranslationClientException exception)
        {
            ConnectionTestState = ConnectionTestState.Error;
            ConnectionTestMessage = GetConnectionErrorMessage(exception.Code);
        }
        catch (Exception)
        {
            ConnectionTestState = ConnectionTestState.Error;
            ConnectionTestMessage = "连接失败。请检查网络和服务地址后重试。";
        }
        finally
        {
            delayCancellation.Cancel();
            IsConnectionTestLoading = false;
            IsConnectionTestRunning = false;
        }
    }

    private bool ValidateAll()
    {
        _errors.Clear();
        foreach (var issue in SettingsValidator.Validate(BuildSettings()))
        {
            var field = NormalizeValidationField(issue.Field);
            _errors.TryAdd(field, GetLocalizedValidationMessage(issue));
        }

        PublishErrors();
        return _errors.Count == 0;
    }

    private bool ValidateModelFields()
    {
        var modelFields = _errors.Keys
            .Where(field => field.StartsWith("Model.", StringComparison.Ordinal))
            .ToArray();
        foreach (var field in modelFields)
        {
            _errors.Remove(field);
        }

        foreach (var issue in SettingsValidator.Validate(BuildSettings())
                     .Where(issue => issue.Field.StartsWith("Model.", StringComparison.Ordinal)))
        {
            _errors.TryAdd(issue.Field, GetLocalizedValidationMessage(issue));
        }

        PublishErrors();
        return !_errors.Keys.Any(field => field.StartsWith("Model.", StringComparison.Ordinal));
    }

    private void RollBackPrePersistenceSideEffects()
    {
        try
        {
            _ = _hotkeyService.TryRegister(_activeSettings.General.CaptureHotkey);
        }
        catch (Exception)
        {
        }

        try
        {
            _startupService.SetEnabled(_activeSettings.General.RunAtStartup);
        }
        catch (Exception)
        {
        }
    }

    private void RequestFirstInvalidFieldFocus()
    {
        if (FirstInvalidField is { } fieldName)
        {
            FocusRequested?.Invoke(this, new FocusRequestedEventArgs(fieldName));
        }
    }

    private void PublishErrors()
    {
        FirstInvalidField = _errors.Keys.FirstOrDefault();
        OnPropertyChanged(nameof(Errors));
    }

    private void ClearError(string fieldName)
    {
        if (_errors.Remove(fieldName))
        {
            PublishErrors();
        }
    }

    private void UpdateDirtyState() => IsDirty = BuildSettings() != _activeSettings;

    private AppSettings BuildSettings() => new(
        new GeneralSettings(CaptureHotkey, DefaultTargetLanguage, RunAtStartup, Theme),
        new ModelSettings(
            BaseUrl,
            ApiKey,
            ModelName,
            EnableThinking,
            TryParseDouble(TemperatureText, out var temperature) ? temperature : double.NaN,
            int.TryParse(MaxOutputTokensText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var tokens)
                ? tokens
                : int.MinValue,
            int.TryParse(RequestTimeoutSecondsText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var timeout)
                ? timeout
                : int.MinValue,
            ExtraParametersJson));

    private void SetEditableProperty<T>(
        ref T field,
        T value,
        string validationField,
        [CallerMemberName] string? propertyName = null)
    {
        if (!SetProperty(ref field, value, propertyName))
        {
            return;
        }

        ClearError(validationField);
        PageError = null;
        SaveStatusMessage = null;
        UpdateDirtyState();
    }

    private void EditablePropertyChanged(string validationField)
    {
        ClearError(validationField);
        PageError = null;
        SaveStatusMessage = null;
        UpdateDirtyState();
    }

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

    private static string NormalizeValidationField(string fieldName) =>
        fieldName.StartsWith("General.CaptureHotkey", StringComparison.Ordinal)
            ? "General.CaptureHotkey"
            : fieldName;

    private static bool TryParseDouble(string value, out double result) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.CurrentCulture, out result) ||
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result);

    private static string FormatNumber(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);

    private static string GetLocalizedValidationMessage(ValidationIssue issue) => issue.Field switch
    {
        "General.CaptureHotkey.Modifiers" => "快捷键必须包含 Ctrl、Alt、Shift 或 Windows 修饰键。",
        "General.CaptureHotkey.VirtualKey" => "快捷键必须包含一个非修饰键。",
        "Model.BaseUrl" => "请输入完整的 HTTP 或 HTTPS 服务地址。",
        "Model.ApiKey" => "请输入 API Key。",
        "Model.ModelName" => "请输入模型名称。",
        "Model.Temperature" => "Temperature 必须在 0 到 2 之间。",
        "Model.MaxOutputTokens" => "最大输出长度必须在 64 到 8192 之间。",
        "Model.RequestTimeoutSeconds" => "请求超时必须在 5 到 120 秒之间。",
        "Model.ExtraParametersJson" => "额外请求参数必须是 JSON 对象，例如 {}。",
        _ => issue.Message
    };

    private static string GetConnectionErrorMessage(TranslationErrorCode errorCode) => errorCode switch
    {
        TranslationErrorCode.Unauthorized => "连接失败。请检查 API Key、模型名称和服务权限后重试。",
        TranslationErrorCode.RateLimited => "连接失败。请求频率或额度受限，请稍后重试。",
        TranslationErrorCode.Timeout => "连接超时。请检查网络或增大请求超时后重试。",
        TranslationErrorCode.ServiceUnavailable => "模型服务暂时不可用，请稍后重试。",
        TranslationErrorCode.InvalidResponse => "服务响应格式不兼容，请检查模型和接口地址后重试。",
        _ => "连接失败。请检查网络和服务地址后重试。"
    };
}

public sealed class FocusRequestedEventArgs(string fieldName) : EventArgs
{
    public string FieldName { get; } = fieldName;
}

public sealed record ThemeOption(AppTheme Value, string DisplayName);
