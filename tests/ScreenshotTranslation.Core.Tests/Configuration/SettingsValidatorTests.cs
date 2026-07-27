using ScreenshotTranslation.Core.Configuration;

namespace ScreenshotTranslation.Core.Tests.Configuration;

public sealed class SettingsValidatorTests
{
    [Fact]
    public void Defaults_match_the_approved_spec()
    {
        var settings = AppSettings.CreateDefault();

        Assert.Equal("https://dashscope.aliyuncs.com/compatible-mode/v1", settings.Model.BaseUrl);
        Assert.Equal("qwen3.7-flash", settings.Model.ModelName);
        Assert.False(settings.Model.EnableThinking);
        Assert.Equal("zh-CN", settings.General.DefaultTargetLanguage);
        Assert.Equal(new HotkeyGesture(HotkeyModifiers.Control | HotkeyModifiers.Alt, 0x44), settings.General.CaptureHotkey);
    }

    [Fact]
    public void Validate_reports_each_recoverable_field_error()
    {
        var settings = AppSettings.CreateDefault() with
        {
            Model = AppSettings.CreateDefault().Model with
            {
                BaseUrl = "not-a-url",
                ApiKey = "",
                ModelName = "",
                Temperature = 3,
                MaxOutputTokens = 1,
                RequestTimeoutSeconds = 1,
                ExtraParametersJson = "[]"
            }
        };

        var fields = SettingsValidator.Validate(settings).Select(issue => issue.Field).ToHashSet();

        Assert.Contains("Model.BaseUrl", fields);
        Assert.Contains("Model.ApiKey", fields);
        Assert.Contains("Model.ModelName", fields);
        Assert.Contains("Model.Temperature", fields);
        Assert.Contains("Model.MaxOutputTokens", fields);
        Assert.Contains("Model.RequestTimeoutSeconds", fields);
        Assert.Contains("Model.ExtraParametersJson", fields);
    }
}
