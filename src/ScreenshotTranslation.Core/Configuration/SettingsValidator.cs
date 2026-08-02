using System.Text.Json;
using ScreenshotTranslation.Core.Translation;

namespace ScreenshotTranslation.Core.Configuration;

public sealed record ValidationIssue(string Field, string Message);

public static class SettingsValidator
{
    private const HotkeyModifiers KnownModifiers =
        HotkeyModifiers.Alt | HotkeyModifiers.Control | HotkeyModifiers.Shift | HotkeyModifiers.Windows;

    private static readonly HashSet<int> ModifierVirtualKeys =
    [
        0x10, // VK_SHIFT
        0x11, // VK_CONTROL
        0x12, // VK_MENU
        0x5B, // VK_LWIN
        0x5C, // VK_RWIN
        0xA0, // VK_LSHIFT
        0xA1, // VK_RSHIFT
        0xA2, // VK_LCONTROL
        0xA3, // VK_RCONTROL
        0xA4, // VK_LMENU
        0xA5  // VK_RMENU
    ];

    public static IReadOnlyList<ValidationIssue> Validate(AppSettings settings)
    {
        return Validate(settings, requireApiKey: true);
    }

    public static IReadOnlyList<ValidationIssue> ValidatePersisted(AppSettings settings)
    {
        return Validate(settings, requireApiKey: false);
    }

    private static IReadOnlyList<ValidationIssue> Validate(
        AppSettings settings,
        bool requireApiKey)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var issues = new List<ValidationIssue>();

        if (settings.General is null)
        {
            issues.Add(new ValidationIssue("General", "General settings are required."));
        }
        else
        {
            ValidateGeneral(settings.General, issues);
        }

        if (settings.Model is null)
        {
            issues.Add(new ValidationIssue("Model", "Model settings are required."));
        }
        else
        {
            ValidateModel(settings.Model, issues, requireApiKey);
        }

        return issues;
    }

    private static void ValidateGeneral(GeneralSettings general, ICollection<ValidationIssue> issues)
    {
        if (general.CaptureHotkey is null)
        {
            issues.Add(new ValidationIssue(
                "General.CaptureHotkey",
                "The capture hotkey is required."));
        }
        else
        {
            if (general.CaptureHotkey.Modifiers == HotkeyModifiers.None ||
                (general.CaptureHotkey.Modifiers & ~KnownModifiers) != HotkeyModifiers.None)
            {
                issues.Add(new ValidationIssue(
                    "General.CaptureHotkey.Modifiers",
                    "The capture hotkey must use at least one supported modifier."));
            }

            if (general.CaptureHotkey.VirtualKey is <= 0 or > 0xFF ||
                ModifierVirtualKeys.Contains(general.CaptureHotkey.VirtualKey))
            {
                issues.Add(new ValidationIssue(
                    "General.CaptureHotkey.VirtualKey",
                    "The capture hotkey must use a non-modifier virtual key."));
            }
        }

        if (!LanguageCatalog.IsSupported(general.DefaultTargetLanguage))
        {
            issues.Add(new ValidationIssue(
                "General.DefaultTargetLanguage",
                "The default target language must be supported."));
        }

        if (!Enum.IsDefined(typeof(AppTheme), general.Theme))
        {
            issues.Add(new ValidationIssue(
                "General.Theme",
                "The selected theme is invalid."));
        }
    }

    private static void ValidateModel(
        ModelSettings model,
        ICollection<ValidationIssue> issues,
        bool requireApiKey)
    {
        if (!Uri.TryCreate(model.BaseUrl, UriKind.Absolute, out var baseUri) ||
            (baseUri.Scheme != Uri.UriSchemeHttp && baseUri.Scheme != Uri.UriSchemeHttps))
        {
            issues.Add(new ValidationIssue("Model.BaseUrl", "Base URL must be an absolute HTTP or HTTPS URL."));
        }

        if (model.ApiKey is null || (requireApiKey && string.IsNullOrWhiteSpace(model.ApiKey)))
        {
            issues.Add(new ValidationIssue("Model.ApiKey", "API key is required."));
        }

        if (string.IsNullOrWhiteSpace(model.ModelName))
        {
            issues.Add(new ValidationIssue("Model.ModelName", "Model name is required."));
        }

        if (!double.IsFinite(model.Temperature) || model.Temperature is < 0 or > 2)
        {
            issues.Add(new ValidationIssue("Model.Temperature", "Temperature must be between 0 and 2."));
        }

        if (model.MaxOutputTokens is < 64 or > 8192)
        {
            issues.Add(new ValidationIssue("Model.MaxOutputTokens", "Maximum output tokens must be between 64 and 8192."));
        }

        if (model.RequestTimeoutSeconds is < 5 or > 120)
        {
            issues.Add(new ValidationIssue("Model.RequestTimeoutSeconds", "Request timeout must be between 5 and 120 seconds."));
        }

        if (!IsJsonObject(model.ExtraParametersJson))
        {
            issues.Add(new ValidationIssue("Model.ExtraParametersJson", "Extra parameters must be a JSON object."));
        }
    }

    private static bool IsJsonObject(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(value);
            return document.RootElement.ValueKind == JsonValueKind.Object;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
