namespace ScreenshotTranslation.Core.Translation;

public sealed record LanguageOption(string Code, string DisplayName, string PromptName);

public static class LanguageCatalog
{
    public static IReadOnlyList<LanguageOption> All { get; } = Array.AsReadOnly<LanguageOption>(
    [
        new("zh-CN", "简体中文", "Simplified Chinese"),
        new("en", "English", "English"),
        new("ja", "日本語", "Japanese"),
        new("ko", "한국어", "Korean"),
        new("ru", "Русский", "Russian"),
        new("fr", "Français", "French"),
        new("de", "Deutsch", "German"),
        new("es", "Español", "Spanish"),
        new("pt", "Português", "Portuguese"),
        new("it", "Italiano", "Italian"),
        new("th", "ไทย", "Thai"),
        new("vi", "Tiếng Việt", "Vietnamese"),
        new("id", "Bahasa Indonesia", "Indonesian"),
        new("tr", "Türkçe", "Turkish"),
        new("ar", "العربية", "Arabic")
    ]);
}
