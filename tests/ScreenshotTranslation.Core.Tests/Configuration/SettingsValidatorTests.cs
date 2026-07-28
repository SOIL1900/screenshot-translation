using ScreenshotTranslation.Core.Configuration;

namespace ScreenshotTranslation.Core.Tests.Configuration;

public sealed class SettingsValidatorTests
{
    [Fact]
    public void Defaults_match_the_approved_spec()
    {
        var settings = AppSettings.CreateDefault();

        Assert.Equal("https://dashscope.aliyuncs.com/compatible-mode/v1", settings.Model.BaseUrl);
        Assert.Equal(string.Empty, settings.Model.ApiKey);
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

    [Fact]
    public void Validate_reports_a_hotkey_without_modifiers()
    {
        var settings = AppSettings.CreateDefault() with
        {
            General = AppSettings.CreateDefault().General with
            {
                CaptureHotkey = new HotkeyGesture(HotkeyModifiers.None, 0x44)
            }
        };

        var fields = SettingsValidator.Validate(settings).Select(issue => issue.Field).ToHashSet();

        Assert.Contains("General.CaptureHotkey.Modifiers", fields);
        Assert.DoesNotContain("General.CaptureHotkey.VirtualKey", fields);
    }

    [Fact]
    public void Validate_reports_a_modifier_virtual_key()
    {
        var settings = AppSettings.CreateDefault() with
        {
            General = AppSettings.CreateDefault().General with
            {
                CaptureHotkey = new HotkeyGesture(HotkeyModifiers.Control, 0x11)
            }
        };

        var fields = SettingsValidator.Validate(settings).Select(issue => issue.Field).ToHashSet();

        Assert.DoesNotContain("General.CaptureHotkey.Modifiers", fields);
        Assert.Contains("General.CaptureHotkey.VirtualKey", fields);
    }

    [Fact]
    public void Validate_accepts_control_alt_d()
    {
        var settings = AppSettings.CreateDefault() with
        {
            General = AppSettings.CreateDefault().General with
            {
                CaptureHotkey = new HotkeyGesture(HotkeyModifiers.Control | HotkeyModifiers.Alt, 0x44)
            }
        };

        var fields = SettingsValidator.Validate(settings).Select(issue => issue.Field).ToHashSet();

        Assert.DoesNotContain("General.CaptureHotkey.Modifiers", fields);
        Assert.DoesNotContain("General.CaptureHotkey.VirtualKey", fields);
    }

    [Fact]
    public void Persisted_validation_allows_the_first_run_empty_api_key()
    {
        var issues = SettingsValidator.ValidatePersisted(AppSettings.CreateDefault());

        Assert.DoesNotContain(issues, issue => issue.Field == "Model.ApiKey");
        Assert.Empty(issues);
    }
}
