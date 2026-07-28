namespace ScreenshotTranslation.Core.Translation;

public static class TranslationPrompts
{
    public static string CreateScreenshotPrompt(string targetLanguageCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetLanguageCode);

        return $$"""
            Translate the readable game-chat text in the attached screenshot into {{targetLanguageCode}}.
            Detect the main source language and translate every readable chat message.
            Preserve usernames, message order, and line breaks.
            Interpret abbreviations, slang, and colloquial language in the context of game chat.
            Return no explanations, analysis, Markdown, or unrelated content.
            Return exactly one JSON object with this shape:
            {"status":"ok","sourceLanguage":"English name of detected language","sourceLanguageCode":"BCP-47 language code","translation":"translated chat"}
            If there is no readable text to translate, return exactly:
            {"status":"no_text","sourceLanguage":"","sourceLanguageCode":"","translation":""}
            """;
    }

    public static string CreateReplyPrompt(string input, string targetLanguageCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(input);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetLanguageCode);

        return $$"""
            Translate the text between <message> tags into {{targetLanguageCode}}.
            Use concise, natural language suitable for game chat.
            Return only the translation with no explanation, analysis, labels, quotation marks, or Markdown.
            <message>{{input}}</message>
            """;
    }

    public const string ConnectionTest =
        "Reply with exactly OK and no other text.";
}
