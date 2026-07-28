namespace ScreenshotTranslation.Core.Configuration;

public enum AppTheme
{
    System,
    Light,
    Dark
}

public sealed record GeneralSettings(
    HotkeyGesture CaptureHotkey,
    string DefaultTargetLanguage,
    bool RunAtStartup,
    AppTheme Theme);

public sealed record ModelSettings(
    string BaseUrl,
    string ApiKey,
    string ModelName,
    bool EnableThinking,
    double Temperature,
    int MaxOutputTokens,
    int RequestTimeoutSeconds,
    string ExtraParametersJson);

public sealed record AppSettings(GeneralSettings General, ModelSettings Model)
{
    public static AppSettings CreateDefault() => new(
        new GeneralSettings(HotkeyGesture.Default, "zh-CN", false, AppTheme.System),
        new ModelSettings(
            "https://dashscope.aliyuncs.com/compatible-mode/v1",
            string.Empty,
            "qwen3.7-flash",
            false,
            0.2,
            2048,
            30,
            "{}"));
}
